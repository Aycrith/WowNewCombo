[CmdletBinding()]
param(
    [int]$Port = 5055,
    [switch]$EnableHazards,
    [switch]$EnableDebugMode,
    [switch]$OpenLeaflet,
    [int]$MapId = 0,
    [int]$DurationSeconds = 60
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$flagsPath = Join-Path $repoRoot 'BlazorServer\runtime_feature_flags.json'

if (-not (Test-Path $flagsPath)) {
    throw "Missing feature flags file: $flagsPath"
}

function Invoke-Get([string]$Url) {
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -TimeoutSec 10 $Url
        $content = if ($null -ne $resp.Content) { "$($resp.Content)" } else { "" }
        return @{ StatusCode = $resp.StatusCode; ContentLength = $content.Length }
    } catch {
        return @{ StatusCode = -1; ContentLength = 0; Error = $_.Exception.Message }
    }
}

function Write-Flags([bool]$hazardEnabled, [bool]$debugMode) {
    $orig = Get-Content -Path $flagsPath -Raw
    $obj = $orig | ConvertFrom-Json
    $obj.Features.HazardAvoidance.Enabled = $hazardEnabled
    $obj.DebugMode = $debugMode
    $obj.LastModified = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $obj | ConvertTo-Json -Depth 64 | Set-Content -Path $flagsPath -Encoding UTF8
    return $orig
}

$origFlags = $null
$proc = $null

try {
    if ($EnableHazards -or $EnableDebugMode) {
        $origFlags = Write-Flags -hazardEnabled ([bool]$EnableHazards) -debugMode ([bool]$EnableDebugMode)
        Write-Host ("runtime_feature_flags.json updated (HazardAvoidance={0}, DebugMode={1})" -f $EnableHazards, $EnableDebugMode)
    }

    Write-Host "Building BlazorServer (Release)..."
    dotnet build "$repoRoot\\BlazorServer\\BlazorServer.csproj" -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed (exitCode=$LASTEXITCODE)."
    }

    Write-Host ("Starting BlazorServer on http://localhost:{0}" -f $Port)
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList "run --project BlazorServer -c Release --no-build --urls http://localhost:$Port" -PassThru -WindowStyle Hidden

    $healthUrl = "http://localhost:$Port/api/health"
    $leafletUrl = "http://localhost:$Port/Leaflet"
    $hazMapsUrl = "http://localhost:$Port/api/debug/hazards/maps"
    $hazSnapUrl = "http://localhost:$Port/api/debug/hazards/${MapId}?includeEvents=true&includeClusters=true&maxEvents=50&maxClusters=50&mostRecentFirst=true"

    $deadline = (Get-Date).AddSeconds(30)
    while ($true) {
        $h = Invoke-Get $healthUrl
        if ($h.StatusCode -eq 200) { break }
        if ((Get-Date) -gt $deadline) { throw "Server did not become healthy within 30 seconds (lastStatus=$($h.StatusCode))." }
        Start-Sleep -Milliseconds 250
    }

    Write-Host "Server healthy."
    Write-Host ("GET {0} => {1}" -f $hazMapsUrl, (Invoke-Get $hazMapsUrl).StatusCode)
    Write-Host ("GET {0} => {1}" -f $hazSnapUrl, (Invoke-Get $hazSnapUrl).StatusCode)
    Write-Host ("GET {0} => {1}" -f $leafletUrl, (Invoke-Get $leafletUrl).StatusCode)

    if ($OpenLeaflet) {
        Write-Host ("Opening: {0}" -f $leafletUrl)
        Start-Process $leafletUrl | Out-Null
    }

    Write-Host ""
    Write-Host ("Monitoring for {0}s (Ctrl+C to stop early)." -f $DurationSeconds)
    Write-Host ("PID={0}" -f $proc.Id)
    Write-Host ""

    $until = (Get-Date).AddSeconds([Math]::Max(1, $DurationSeconds))
    while ((Get-Date) -lt $until) {
        Start-Sleep -Seconds 5
        $h = Invoke-Get $healthUrl
        $snap = Invoke-Get $hazSnapUrl
        Write-Host ("{0:u} health={1} hazards={2}" -f (Get-Date).ToUniversalTime(), $h.StatusCode, $snap.StatusCode)
    }
} finally {
    if ($proc -ne $null) {
        try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
    }

    if ($origFlags -ne $null) {
        Set-Content -Path $flagsPath -Value $origFlags -Encoding UTF8
        Write-Host "Restored original runtime_feature_flags.json"
    }
}

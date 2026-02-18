param(
    [int]$Port = 5055,
    [int]$MapId = 0,
    [float]$X = 0,
    [float]$Y = 0,
    [float]$Z = 0,
    [switch]$OpenLeaflet,
    [int]$HoldSeconds = 0,
    [switch]$TryPathRoute,
    [switch]$TryPathCompare,
    [float]$ToX = 0,
    [float]$ToY = 0,
    [float]$ToZ = 0
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$flagsPath = Join-Path $repoRoot 'BlazorServer\runtime_feature_flags.json'

if (-not (Test-Path $flagsPath)) {
    throw "Missing feature flags file: $flagsPath"
}

$originalFlagsJson = Get-Content -Path $flagsPath -Raw

function Write-Flags([bool]$hazardEnabled, [bool]$debugMode) {
    $flags = $originalFlagsJson | ConvertFrom-Json
    $flags.Features.HazardAvoidance.Enabled = $hazardEnabled
    $flags.DebugMode = $debugMode
    $flags.LastModified = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

    $json = $flags | ConvertTo-Json -Depth 64
    Set-Content -Path $flagsPath -Value $json -Encoding UTF8
}

function Invoke-Json([string]$Method, [string]$Url, [object]$Body = $null) {
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $Url -TimeoutSec 10
        }

        $payload = $Body | ConvertTo-Json -Depth 16
        return Invoke-RestMethod -Method $Method -Uri $Url -TimeoutSec 10 -ContentType 'application/json' -Body $payload
    } catch {
        Write-Host ("Request failed: {0} {1}" -f $Method, $Url)
        throw
    }
}

function Invoke-GetStatus([string]$Url) {
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -TimeoutSec 10 $Url
        return [int]$resp.StatusCode
    } catch {
        return -1
    }
}

function Get-PathLength([object]$points) {
    if ($null -eq $points -or $points.Count -lt 2) {
        return 0.0
    }

    $len = 0.0
    for ($i = 1; $i -lt $points.Count; $i++) {
        $dx = [double]($points[$i].X - $points[$i - 1].X)
        $dy = [double]($points[$i].Y - $points[$i - 1].Y)
        $dz = [double]($points[$i].Z - $points[$i - 1].Z)
        $len += [Math]::Sqrt(($dx * $dx) + ($dy * $dy) + ($dz * $dz))
    }

    return $len
}

Write-Host "Enabling DebugMode + HazardAvoidance in runtime flags (temporary)"
Write-Flags -hazardEnabled $true -debugMode $true

$proc = $null

try {
    Write-Host "Building BlazorServer (Release) to ensure latest Frontend controllers are included..."
    dotnet build "$repoRoot\\BlazorServer\\BlazorServer.csproj" -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed (exitCode=$LASTEXITCODE)."
    }

    Write-Host ""
    Write-Host "Starting BlazorServer on http://localhost:$Port"
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList "run --project BlazorServer -c Release --no-build --urls http://localhost:$Port" -PassThru -WindowStyle Hidden

    $healthUrl = "http://localhost:$Port/api/health"
    $mapsUrl = "http://localhost:$Port/api/debug/hazards/maps"
    $clearUrl = "http://localhost:$Port/api/debug/hazards/$MapId/clear"
    $injectUrl = "http://localhost:$Port/api/debug/hazards/$MapId/inject"
    $clusterUrl = "http://localhost:$Port/api/debug/hazards/$MapId/cluster"
    $snapshotUrl = "http://localhost:$Port/api/debug/hazards/${MapId}?includeEvents=true&includeClusters=true&maxEvents=20&maxClusters=20&mostRecentFirst=true"
    $leafletUrl = "http://localhost:$Port/Leaflet"
    $pathRouteUrl = "http://localhost:$Port/api/debug/path/$MapId/route"
    $pathCompareUrl = "http://localhost:$Port/api/debug/path/$MapId/compare"

    $deadline = (Get-Date).AddSeconds(25)
    while ($true) {
        try {
            $null = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2
            break
        } catch {
            if ((Get-Date) -gt $deadline) {
                throw "Server did not become healthy within 25 seconds."
            }
            Start-Sleep -Milliseconds 250
        }
    }

    Write-Host "Server healthy. Clearing + injecting synthetic hazard events..."
    $null = Invoke-Json -Method 'POST' -Url $clearUrl

    $inject = @{
        x = $X
        y = $Y
        z = $Z
        uiMapId = 0
        type = 99
        count = 5
        zone = 'Validate-HazardAvoidance'
        ageMinutes = 1
    }

    $injectResp = Invoke-Json -Method 'POST' -Url $injectUrl -Body $inject
    Write-Host ("Injected: AddedEvents={0} TotalEvents={1}" -f $injectResp.addedEvents, $injectResp.totalEvents)

    $clusterResp = Invoke-Json -Method 'POST' -Url $clusterUrl
    Write-Host ("Clustered: TotalEvents={0} ClusterCount={1} (eps={2} minPts={3})" -f $clusterResp.totalEvents, $clusterResp.clusterCount, $clusterResp.epsilon, $clusterResp.minPoints)

    $snapshot = Invoke-Json -Method 'GET' -Url $snapshotUrl
    Write-Host ("Snapshot: TotalEvents={0} TotalClusters={1} ReturnedEvents={2} ReturnedClusters={3}" -f $snapshot.totalEventCount, $snapshot.totalClusterCount, $snapshot.events.Count, $snapshot.clusters.Count)

    Write-Host ("Leaflet page check: GET {0} => {1}" -f $leafletUrl, (Invoke-GetStatus $leafletUrl))

    if ($OpenLeaflet) {
        Write-Host ("Opening: {0}" -f $leafletUrl)
        Start-Process $leafletUrl | Out-Null
    }

    if ($TryPathRoute) {
        $toXValue = $ToX
        $toYValue = $ToY
        $toZValue = $ToZ
        if ($toXValue -eq 0 -and $toYValue -eq 0 -and $toZValue -eq 0) {
            $toXValue = $X + 50
            $toYValue = $Y
            $toZValue = $Z
        }

        $routeReq = @{
            fromX = $X
            fromY = $Y
            fromZ = $Z
            toX = $toXValue
            toY = $toYValue
            toZ = $toZValue
        }

        try {
            $routeResp = Invoke-Json -Method 'POST' -Url $pathRouteUrl -Body $routeReq
            Write-Host ("PathDebug: PointCount={0} MaxHazardCost={1}" -f $routeResp.pointCount, $routeResp.maxHazardCost)
        } catch {
            Write-Host "PathDebug: failed (this is expected if MPQ data is missing)."
        }
    }

    if ($TryPathCompare) {
        $toXValue = $ToX
        $toYValue = $ToY
        $toZValue = $ToZ
        if ($toXValue -eq 0 -and $toYValue -eq 0 -and $toZValue -eq 0) {
            $toXValue = $X + 50
            $toYValue = $Y
            $toZValue = $Z
        }

        $compareReq = @{
            fromX = $X
            fromY = $Y
            fromZ = $Z
            toX = $toXValue
            toY = $toYValue
            toZ = $toZValue
            hazardStrategy = 'A_Star_With_Model_Avoidance'
            baselineStrategy = 'A_Star'
        }

        try {
            $cmp = Invoke-Json -Method 'POST' -Url $pathCompareUrl -Body $compareReq
            $hazLen = Get-PathLength $cmp.hazardPath.points
            $baseLen = Get-PathLength $cmp.baselinePath.points
            Write-Host ("PathCompare: HazardPoints={0} HazardLen={1:N1} BasePoints={2} BaseLen={3:N1}" -f $cmp.hazardPath.pointCount, $hazLen, $cmp.baselinePath.pointCount, $baseLen)
        } catch {
            Write-Host "PathCompare: failed (this is expected if MPQ data is missing)."
        }
    }

    Write-Host ""
    Write-Host "Next manual checks:"
    Write-Host "- Open the UI Leaflet page and toggle 'Hazards' to see the heat overlay."
    Write-Host "- If using local PPather, verify routes deviate after hazards are present."
    Write-Host ""
    Write-Host "Endpoints:"
    Write-Host "- $mapsUrl"
    Write-Host "- $snapshotUrl"
    Write-Host "- $leafletUrl"

    if ($HoldSeconds -gt 0) {
        Write-Host ""
        Write-Host ("Holding server for {0}s for manual UI checks..." -f $HoldSeconds)
        Start-Sleep -Seconds $HoldSeconds
    }
} finally {
    if ($proc -ne $null) {
        try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
    }

    Write-Host "Restoring original runtime_feature_flags.json"
    Set-Content -Path $flagsPath -Value $originalFlagsJson -Encoding UTF8
}

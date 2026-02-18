param(
    [int]$Port = 5055,
    [string]$BotPath = "C:\\WowClassicGrindBot",
    [int]$MapId = 0,
    [switch]$OpenLeaflet,
    [int]$HoldSeconds = 0
)

$ErrorActionPreference = 'Stop'

$repoRoot = $BotPath
if (-not (Test-Path (Join-Path $repoRoot "MasterOfPuppets.sln"))) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
}

function Invoke-GetStatus([string]$Url) {
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -TimeoutSec 10 $Url
        return [int]$resp.StatusCode
    } catch {
        return -1
    }
}

Write-Host "Preflight: repoRoot=$repoRoot"

$routesDir = Join-Path $repoRoot "Json\\Routes"
$classDir = Join-Path $repoRoot "Json\\class"
$navExe = Join-Path $repoRoot "Navigation\\AmeisenNavigationServer.exe"
$flagsPath = Join-Path $repoRoot "BlazorServer\\runtime_feature_flags.json"

Write-Host ""
Write-Host "Files/Folders:"
Write-Host ("- runtime_feature_flags.json: {0}" -f (Test-Path $flagsPath))
Write-Host ("- Routes dir: {0}" -f (Test-Path $routesDir))
Write-Host ("- Class dir:  {0}" -f (Test-Path $classDir))
Write-Host ("- Navigation exe: {0}" -f (Test-Path $navExe))

if (Test-Path $routesDir) {
    $routes = Get-ChildItem -Path $routesDir -File -ErrorAction SilentlyContinue | Select-Object -First 10
    Write-Host ("Routes (first {0}):" -f $routes.Count)
    foreach ($r in $routes) { Write-Host ("- {0}" -f $r.Name) }
}

if (Test-Path $classDir) {
    $profiles = Get-ChildItem -Path $classDir -File -ErrorAction SilentlyContinue | Select-Object -First 10
    Write-Host ("Class profiles (first {0}):" -f $profiles.Count)
    foreach ($p in $profiles) { Write-Host ("- {0}" -f $p.Name) }
}

Write-Host ""
Write-Host "Building BlazorServer (Release)..."
dotnet build "$repoRoot\\BlazorServer\\BlazorServer.csproj" -c Release | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed (exitCode=$LASTEXITCODE)."
}

Write-Host ""
Write-Host "Starting BlazorServer..."
$proc = Start-Process -FilePath 'dotnet' -ArgumentList "run --project BlazorServer -c Release --no-build --urls http://localhost:$Port" -PassThru -WindowStyle Hidden

try {
    $healthUrl = "http://localhost:$Port/api/health"
    $readinessUrl = "http://localhost:$Port/api/launch/status"
    $leafletUrl = "http://localhost:$Port/Leaflet"
    $hazUrl = "http://localhost:$Port/api/debug/hazards/${MapId}?includeEvents=true&includeClusters=true&maxEvents=5&maxClusters=5"

    $deadline = (Get-Date).AddSeconds(30)
    while ((Invoke-GetStatus $healthUrl) -ne 200) {
        if ((Get-Date) -gt $deadline) { throw "Server did not become healthy within 30s." }
        Start-Sleep -Milliseconds 250
    }

    Write-Host "HTTP checks:"
    Write-Host ("- GET /api/health => {0}" -f (Invoke-GetStatus $healthUrl))
    Write-Host ("- GET /api/launch/status => {0}" -f (Invoke-GetStatus $readinessUrl))
    Write-Host ("- GET /Leaflet => {0}" -f (Invoke-GetStatus $leafletUrl))
    Write-Host ("- GET /api/debug/hazards/{0} => {1}" -f $MapId, (Invoke-GetStatus $hazUrl))

    if ($OpenLeaflet) {
        Write-Host ("Opening: {0}" -f $leafletUrl)
        Start-Process $leafletUrl | Out-Null
    }

    if ($HoldSeconds -gt 0) {
        Write-Host ("Holding server for {0}s..." -f $HoldSeconds)
        Start-Sleep -Seconds $HoldSeconds
    }
} finally {
    try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
}

Write-Host ""
Write-Host "Preflight complete."

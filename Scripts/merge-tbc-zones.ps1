param(
    [string]$SomPath = "C:\WowClassicGrindBot\Json\dbc\som\WorldMapArea.json",
    [string]$TbcPath = "C:\WowClassicGrindBot\Json\dbc\tbc\WorldMapArea.json"
)
$somRaw = Get-Content $SomPath -Raw | ConvertFrom-Json
$tbcRaw = Get-Content $TbcPath -Raw | ConvertFrom-Json
$somIds = @{}
foreach ($entry in $somRaw) { $somIds[$entry.UIMapId] = $true }
$missing = @()
foreach ($entry in $tbcRaw) {
    if ($entry.UIMapId -ne 0 -and -not $somIds.ContainsKey($entry.UIMapId)) { $missing += $entry }
}
Write-Host "SoM: $($somRaw.Count), Missing TBC: $($missing.Count)"
$merged = @($somRaw) + $missing
$merged | ConvertTo-Json -Depth 5 | Set-Content $SomPath -Encoding UTF8
Write-Host "Saved. Total: $($merged.Count)"
$ghost = $merged | Where-Object { $_.AreaName -eq "Ghostlands" }
$ever = $merged | Where-Object { $_.AreaName -eq "Eversong Woods" }
if ($ghost) { Write-Host "Ghostlands OK: UIMapId=$($ghost.UIMapId)" } else { Write-Host "Ghostlands MISSING!" }
if ($ever) { Write-Host "Eversong OK: UIMapId=$($ever.UIMapId)" } else { Write-Host "Eversong MISSING!" }

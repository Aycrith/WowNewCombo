[CmdletBinding()]
param(
    [int]$Bytes = 30000,
    [int]$Seconds = 0,
    [Alias("h", "?")][switch]$Help
)

if ($Help)
{
    Write-Host "Usage: monitor-pull.ps1 [-Bytes <n>] [-Seconds <n>]" -ForegroundColor Cyan
    Write-Host "When -Seconds is 0, prints one-shot filtered log tail; otherwise runs live filtered tail for N seconds." -ForegroundColor Gray
    return
}

$logPath = (Get-ChildItem C:\WowClassicGrindBot -Recurse -Filter "out*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

$keywords = "PullTarget|GoapAgent.*Plan|CastingHandler|Corruption|Immolate|Shadow.Bolt|Curse.of.Agony|SPELL_FAILED|OUT_OF_RANGE|rangedPull|failed.*confirm|body-pull|FollowRoute|ApproachTarget|CombatGoal|New Plan"

function Read-TailMatches([int]$fromPos) {
    $fs = [System.IO.File]::Open($logPath, 'Open', 'Read', 'ReadWrite')
    $endPos = $fs.Seek(0, 'End')
    if ($fromPos -lt 0) { $fromPos = [Math]::Max(0, $endPos + $fromPos) }
    $fs.Seek($fromPos, 'Begin') | Out-Null
    $sr = New-Object System.IO.StreamReader($fs)
    $content = $sr.ReadToEnd(); $sr.Close(); $fs.Close()
    return @{ Lines = ($content -split "`n"); EndPos = $endPos }
}

if ($Seconds -eq 0) {
    # One-shot: show last $Bytes worth of relevant lines
    $result = Read-TailMatches(-$Bytes)
    $result.Lines | Where-Object { $_ -match $keywords } | ForEach-Object {
        $color = if ($_ -match "failed|FAILED|SPELL_FAILED|OUT_OF_RANGE|refused|refusing|body-pull") { "Red" }
                 elseif ($_ -match "Corruption|Immolate|Shadow.Bolt|Curse") { "Cyan" }
                 elseif ($_ -match "New Plan|GoapAgent") { "Green" }
                 elseif ($_ -match "approach|Approach|FollowRoute") { "Yellow" }
                 else { "White" }
        Write-Host $_ -ForegroundColor $color
    }
} else {
    # Live tail mode
    $startPos = (Read-TailMatches(0)).EndPos
    Write-Host "=== Live tail active for ${Seconds}s ===" -ForegroundColor Cyan
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $result = Read-TailMatches($startPos)
        $result.Lines | Where-Object { $_ -match $keywords } | ForEach-Object {
            $color = if ($_ -match "failed|FAILED|SPELL_FAILED|OUT_OF_RANGE|refusing|body-pull") { "Red" }
                     elseif ($_ -match "Corruption|Immolate|Shadow.Bolt|Curse") { "Cyan" }
                     elseif ($_ -match "New Plan|GoapAgent") { "Green" }
                     elseif ($_ -match "approach|Approach|FollowRoute") { "Yellow" }
                     else { "White" }
            Write-Host $_ -ForegroundColor $color
        }
        $startPos = $result.EndPos
        Start-Sleep -Milliseconds 600
    }
    Write-Host "=== Tail ended ===" -ForegroundColor Cyan
}

#Requires -Version 7.0
<#
.SYNOPSIS
    Autonomous test runner for WowClassicGrindBot
.DESCRIPTION
    Runs comprehensive diagnostics, applies fixes automatically, and validates bot functionality
    without requiring manual intervention. Monitors combat metrics during bot execution.
.PARAMETER BaseUrl
    Base URL of the running BlazorServer (default: http://localhost:5000)
.PARAMETER MaxRetries
    Maximum retry attempts for fixes (default: 3)
.PARAMETER BotCycleDuration
    Duration in seconds to run the bot test cycle (default: 60)
.PARAMETER SkipCombatTests
    Skip manual combat tests (target/ability tests)
.PARAMETER VerboseOutput
    Enable verbose output for debugging
.EXAMPLE
    .\test-autonomous.ps1
.EXAMPLE
    .\test-autonomous.ps1 -BotCycleDuration 120 -MaxRetries 5
#>

param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$MaxRetries = 3,
    [int]$BotCycleDuration = 60,
    [switch]$SkipCombatTests,
    [switch]$VerboseOutput
)

# ===== Configuration =====
$Script:WaitBetweenFixes = 3
$Script:PollInterval = 5
$Script:Report = @{
    Timestamp = Get-Date -Format "o"
    SystemInfo = @{}
    Phases = @()
    Summary = $null
}

# ===== Color Scheme =====
$Colors = @{
    Header = "Cyan"
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "White"
    Metric = "DarkGray"
}

# ===== Helper Functions =====

function Invoke-ApiGet {
    param([string]$Endpoint)
    
    try {
        $uri = "$BaseUrl$Endpoint"
        if ($VerboseOutput) {
            Write-Host "  [GET] $uri" -ForegroundColor $Colors.Metric
        }
        
        $response = Invoke-RestMethod -Uri $uri -Method Get -ErrorAction Stop
        return $response
    }
    catch {
        Write-Host "  ❌ API GET failed: $Endpoint" -ForegroundColor $Colors.Error
        Write-Host "     $_" -ForegroundColor $Colors.Error
        throw
    }
}

function Invoke-ApiPost {
    param(
        [string]$Endpoint,
        [object]$Body = @{}
    )
    
    try {
        $uri = "$BaseUrl$Endpoint"
        if ($VerboseOutput) {
            Write-Host "  [POST] $uri" -ForegroundColor $Colors.Metric
        }
        
        $response = Invoke-RestMethod -Uri $uri -Method Post -Body ($Body | ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop
        return $response
    }
    catch {
        Write-Host "  ❌ API POST failed: $Endpoint" -ForegroundColor $Colors.Error
        Write-Host "     $_" -ForegroundColor $Colors.Error
        throw
    }
}

function Write-Phase {
    param(
        [string]$Name,
        [string]$Icon = "📋"
    )
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
    Write-Host "  $Icon $Name" -ForegroundColor $Colors.Header
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
}

function Add-PhaseResult {
    param(
        [string]$Name,
        [bool]$Pass,
        [string]$Message,
        [object]$Data = @{}
    )
    
    $Script:Report.Phases += @{
        Name = $Name
        Pass = $Pass
        Message = $Message
        Data = $Data
        Timestamp = Get-Date -Format "o"
    }
    
    $icon = if ($Pass) { "✅" } else { "❌" }
    $color = if ($Pass) { $Colors.Success } else { $Colors.Error }
    Write-Host "  $icon $Message" -ForegroundColor $color
}

function Write-Metric {
    param(
        [string]$Name,
        [string]$Value
    )
    
    Write-Host "    $($Name): $Value" -ForegroundColor $Colors.Metric
}

function Calculate-Distance {
    param(
        [object]$Pos1,
        [object]$Pos2
    )
    
    $dx = $Pos1.x - $Pos2.x
    $dy = $Pos1.y - $Pos2.y
    return [Math]::Sqrt($dx * $dx + $dy * $dy)
}

# ===== Test Phases =====

function Test-SystemHealth {
    Write-Phase -Name "Phase 1: System Health Check" -Icon "🏥"
    
    try {
        $status = Invoke-ApiGet "/api/test/status"
        
        if ($status.success) {
            $data = $status.data
            Write-Metric "WoW Process" "✓ Running (PID: $($data.processId))"
            Write-Metric "Screen Size" "$($data.screenWidth)x$($data.screenHeight)"
            Write-Metric "Addon" "✓ DataToColor detected"
            
            Add-PhaseResult -Name "System Health" -Pass $true -Message "System operational" -Data $data
            return @{ Pass = $true; Data = $data }
        }
        else {
            Add-PhaseResult -Name "System Health" -Pass $false -Message "System health check failed" -Data $status
            return @{ Pass = $false; Data = $status }
        }
    }
    catch {
        Add-PhaseResult -Name "System Health" -Pass $false -Message "Failed to connect to server: $_"
        return @{ Pass = $false }
    }
}

function Test-Frames {
    Write-Phase -Name "Phase 2: Frame Detection" -Icon "🖼️"
    
    try {
        $frames = Invoke-ApiGet "/api/test/frames"
        
        if ($frames.success) {
            $validCount = $frames.data.validFrameCount
            $totalCount = $frames.data.totalFrameCount
            $validationMarker = $frames.data.validationMarker
            
            Write-Metric "Valid Frames" "$validCount / $totalCount"
            Write-Metric "Validation Marker" $validationMarker
            
            $pass = $validCount -eq $totalCount -and $validationMarker -eq 323
            Add-PhaseResult -Name "Frame Detection" -Pass $pass `
                -Message "$(if ($pass) { 'All frames valid' } else { 'Frame validation failed' })" `
                -Data $frames.data
            
            return @{ Pass = $pass; Data = $frames.data }
        }
        else {
            Add-PhaseResult -Name "Frame Detection" -Pass $false -Message "Frame check failed" -Data $frames
            return @{ Pass = $false }
        }
    }
    catch {
        Add-PhaseResult -Name "Frame Detection" -Pass $false -Message "Frame test failed: $_"
        return @{ Pass = $false }
    }
}

function Test-Keybindings {
    Write-Phase -Name "Phase 3: Keybinding Diagnostics" -Icon "⌨️"
    
    try {
        $keybinds = Invoke-ApiGet "/api/diagnostics/keybindings"
        
        Write-Metric "Total Bindings" $keybinds.totalBindings
        Write-Metric "Mismatches" $keybinds.mismatchCount
        Write-Metric "Initialized" $keybinds.isInitialized
        
        if ($keybinds.mismatchCount -gt 0) {
            Write-Host ""
            Write-Host "  ⚠️  Keybind Mismatches Detected:" -ForegroundColor $Colors.Warning
            foreach ($mismatch in $keybinds.mismatches) {
                Write-Host "    • $($mismatch.bindingId)" -ForegroundColor $Colors.Warning
                Write-Host "      Expected: $($mismatch.expectedModifier)$($mismatch.expectedKey)" -ForegroundColor $Colors.Metric
                Write-Host "      Actual:   $($mismatch.actualModifier)$($mismatch.actualKey)" -ForegroundColor $Colors.Metric
            }
        }
        
        $pass = $keybinds.mismatchCount -eq 0 -and $keybinds.isInitialized
        Add-PhaseResult -Name "Keybindings" -Pass $pass `
            -Message "$(if ($pass) { 'All keybindings match profile' } else { "$($keybinds.mismatchCount) keybind mismatches detected" })" `
            -Data $keybinds
        
        return @{ Pass = $pass; Data = $keybinds }
    }
    catch {
        Add-PhaseResult -Name "Keybindings" -Pass $false -Message "Keybind check failed: $_"
        return @{ Pass = $false }
    }
}

function Fix-Keybindings {
    Write-Host ""
    Write-Host "  🔧 Applying Keybind Fixes..." -ForegroundColor $Colors.Warning
    
    try {
        # Step 1: Apply default bindings (NumPad, F-keys)
        Write-Host "    • Running /dcbindings..." -ForegroundColor $Colors.Info
        $result1 = Invoke-ApiPost "/api/diagnostics/fix/bindings"
        Write-Metric "Result" $result1.message
        
        Start-Sleep -Seconds 2
        
        # Step 2: Apply number row bindings (1-9, 0, -, =)
        Write-Host "    • Running /dcnumberkeys..." -ForegroundColor $Colors.Info
        $result2 = Invoke-ApiPost "/api/diagnostics/fix/numberkeys"
        Write-Metric "Result" $result2.message
        
        Start-Sleep -Seconds 2
        
        # Step 3: Apply custom actions
        Write-Host "    • Running /dcactions..." -ForegroundColor $Colors.Info
        $result3 = Invoke-ApiPost "/api/diagnostics/fix/actions"
        Write-Metric "Result" $result3.message
        
        return @{ Success = $true; Steps = @($result1, $result2, $result3) }
    }
    catch {
        Write-Host "  ❌ Keybind fix failed: $_" -ForegroundColor $Colors.Error
        return @{ Success = $false; Error = $_ }
    }
}

function Test-ActionBar {
    Write-Phase -Name "Phase 4: Action Bar Diagnostics" -Icon "🎯"
    
    try {
        $actionBar = Invoke-ApiGet "/api/diagnostics/actionbar"
        
        Write-Metric "Issues Found" $actionBar.issueCount
        Write-Metric "Texture Initialized" $actionBar.isTextureInitialized
        
        if ($actionBar.issueCount -gt 0) {
            Write-Host ""
            Write-Host "  ⚠️  Action Bar Issues Detected:" -ForegroundColor $Colors.Warning
            foreach ($issue in $actionBar.issues) {
                $icon = if ($issue.canResolve) { "🔧" } else { "⚠️" }
                Write-Host "    $icon Slot $($issue.slot): $($issue.spellName) - $($issue.status)" -ForegroundColor $(if ($issue.canResolve) { $Colors.Warning } else { $Colors.Error })
            }
        }
        
        $pass = $actionBar.issueCount -eq 0 -and $actionBar.isTextureInitialized
        Add-PhaseResult -Name "Action Bar" -Pass $pass `
            -Message "$(if ($pass) { 'Action bar matches profile' } else { "$($actionBar.issueCount) action bar issues detected" })" `
            -Data $actionBar
        
        return @{ Pass = $pass; Data = $actionBar }
    }
    catch {
        Add-PhaseResult -Name "Action Bar" -Pass $false -Message "Action bar check failed: $_"
        return @{ Pass = $false }
    }
}

function Fix-ActionBar {
    Write-Host ""
    Write-Host "  🔧 Syncing Action Bar..." -ForegroundColor $Colors.Warning
    
    try {
        Write-Host "    • Running ActionBarPopulator.Execute()..." -ForegroundColor $Colors.Info
        $result = Invoke-ApiPost "/api/diagnostics/fix/syncbar"
        Write-Metric "Result" $result.message
        
        Start-Sleep -Seconds 2
        
        return @{ Success = $true; Result = $result }
    }
    catch {
        Write-Host "  ❌ Action bar sync failed: $_" -ForegroundColor $Colors.Error
        return @{ Success = $false; Error = $_ }
    }
}

function Test-CombatBasic {
    Write-Phase -Name "Phase 5: Combat Tests (Manual)" -Icon "⚔️"
    
    try {
        # Test targeting
        Write-Host "  Testing targeting..." -ForegroundColor $Colors.Info
        $targetResult = Invoke-ApiPost "/api/test/combat/target"
        
        if ($targetResult.success) {
            Write-Metric "Targeting" "✓ Target acquired"
        }
        else {
            Write-Metric "Targeting" "✗ Failed to target"
        }
        
        Start-Sleep -Seconds 1
        
        # Test ability use
        Write-Host "  Testing ability use..." -ForegroundColor $Colors.Info
        $abilityResult = Invoke-ApiPost "/api/test/combat/ability" @{ abilityKey = "1" }
        
        if ($abilityResult.success) {
            Write-Metric "Ability Use" "✓ Ability activated"
        }
        else {
            Write-Metric "Ability Use" "✗ Failed to use ability"
        }
        
        $pass = $targetResult.success -and $abilityResult.success
        Add-PhaseResult -Name "Combat Tests" -Pass $pass `
            -Message "$(if ($pass) { 'Combat tests passed' } else { 'Combat tests failed' })" `
            -Data @{ Target = $targetResult; Ability = $abilityResult }
        
        return @{ Pass = $pass; Data = @{ Target = $targetResult; Ability = $abilityResult } }
    }
    catch {
        Add-PhaseResult -Name "Combat Tests" -Pass $false -Message "Combat test failed: $_"
        return @{ Pass = $false }
    }
}

function Test-BotCycle {
    param([int]$DurationSeconds)
    
    Write-Phase -Name "Phase 6: Bot Autonomous Cycle Test" -Icon "🤖"
    
    try {
        Write-Host "  Starting bot for $DurationSeconds second test cycle..." -ForegroundColor $Colors.Info
        $startResult = Invoke-ApiPost "/api/bot/start"
        Write-Metric "Bot Status" $startResult.message
        
        Start-Sleep -Seconds 2
        
        # Collect snapshots
        $snapshots = @()
        $snapshotCount = [Math]::Floor($DurationSeconds / $Script:PollInterval)
        
        Write-Host ""
        Write-Host "  📊 Monitoring bot progress:" -ForegroundColor $Colors.Info
        
        for ($i = 0; $i -lt $snapshotCount; $i++) {
            $elapsed = $i * $Script:PollInterval
            $remaining = $DurationSeconds - $elapsed
            
            Write-Host "  ⏱️  [$elapsed/$DurationSeconds s]" -ForegroundColor $Colors.Metric -NoNewline
            
            $snapshot = Invoke-ApiGet "/api/test/snapshot"
            $snapshots += $snapshot
            
            # Real-time metrics
            $pos = $snapshot.player.mapPosition
            Write-Host "  Pos: ($([Math]::Round($pos.x, 2)), $([Math]::Round($pos.y, 2)))" -NoNewline
            Write-Host "  HP: $($snapshot.player.health)/$($snapshot.player.healthMax)" -NoNewline
            Write-Host "  Combat: $(if ($snapshot.bits.combat) { '⚔️' } else { '  ' })" -NoNewline
            Write-Host "  Target: $(if ($snapshot.bits.target) { '🎯' } else { '  ' })" -ForegroundColor $Colors.Metric
            
            if ($i -lt $snapshotCount - 1) {
                Start-Sleep -Seconds $Script:PollInterval
            }
        }
        
        # Stop bot
        Write-Host ""
        Write-Host "  Stopping bot..." -ForegroundColor $Colors.Info
        $stopResult = Invoke-ApiPost "/api/bot/stop"
        Write-Metric "Bot Status" $stopResult.message
        
        # Analyze results
        Write-Host ""
        Write-Host "  📈 Analyzing results..." -ForegroundColor $Colors.Info
        
        $first = $snapshots[0]
        $last = $snapshots[-1]
        
        $distanceMoved = Calculate-Distance -Pos1 $first.player.mapPosition -Pos2 $last.player.mapPosition
        $combatEntered = ($snapshots | Where-Object { $_.bits.combat }).Count -gt 0
        $targetAcquired = ($snapshots | Where-Object { $_.bits.target }).Count -gt 0
        
        # Calculate HP changes
        $hpValues = $snapshots | ForEach-Object { $_.player.health }
        $maxHp = ($hpValues | Measure-Object -Maximum).Maximum
        $minHp = ($hpValues | Measure-Object -Minimum).Minimum
        $hpLost = $maxHp - $minHp
        
        # Estimate kills (count target health drops to 0)
        $killCount = 0
        for ($i = 1; $i -lt $snapshots.Count; $i++) {
            $prev = $snapshots[$i - 1]
            $curr = $snapshots[$i]
            
            if ($prev.bits.target -and $prev.target.health -gt 0 -and 
                $curr.bits.target -and $curr.target.health -eq 0) {
                $killCount++
            }
        }
        
        # Display metrics
        Write-Metric "Distance Moved" "$([Math]::Round($distanceMoved, 3)) units"
        Write-Metric "Combat Entered" $(if ($combatEntered) { "✓ Yes" } else { "✗ No" })
        Write-Metric "Target Acquired" $(if ($targetAcquired) { "✓ Yes" } else { "✗ No" })
        Write-Metric "HP Lost" "$hpLost HP"
        Write-Metric "Estimated Kills" $killCount
        Write-Metric "Snapshots Collected" $snapshots.Count
        
        $metrics = @{
            SnapshotCount = $snapshots.Count
            DistanceMoved = [Math]::Round($distanceMoved, 3)
            CombatEntered = $combatEntered
            TargetAcquired = $targetAcquired
            HpLost = $hpLost
            EstimatedKills = $killCount
            StartPosition = $first.player.mapPosition
            EndPosition = $last.player.mapPosition
        }
        
        # Pass criteria: Must move AND either enter combat or acquire target
        $pass = $distanceMoved -gt 0.01 -and ($combatEntered -or $targetAcquired)
        
        $message = if ($pass) {
            "✅ Bot working - moved $([Math]::Round($distanceMoved, 2)) units, combat/target detected"
        }
        else {
            if ($distanceMoved -le 0.01) {
                "❌ Bot NOT moving - possible spinning issue"
            }
            else {
                "⚠️ Bot moved but no combat/targeting detected"
            }
        }
        
        Add-PhaseResult -Name "Bot Cycle Test" -Pass $pass -Message $message -Data $metrics
        
        return @{ Pass = $pass; Metrics = $metrics }
    }
    catch {
        # Make sure to stop bot on error
        try {
            Invoke-ApiPost "/api/bot/stop" | Out-Null
        }
        catch {
            # Ignore stop errors
        }
        
        Add-PhaseResult -Name "Bot Cycle Test" -Pass $false -Message "Bot cycle test failed: $_"
        return @{ Pass = $false }
    }
}

# ===== Main Execution =====

try {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
    Write-Host "  🤖 WowClassicGrindBot Autonomous Test Suite" -ForegroundColor $Colors.Header
    Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor $Colors.Info
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
    Write-Host "  Base URL: $BaseUrl" -ForegroundColor $Colors.Metric
    Write-Host "  Max Retries: $MaxRetries" -ForegroundColor $Colors.Metric
    Write-Host "  Bot Cycle Duration: $BotCycleDuration seconds" -ForegroundColor $Colors.Metric
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
    
    # Phase 1: System Health
    $healthResult = Test-SystemHealth
    if (-not $healthResult.Pass) {
        Write-Host ""
        Write-Host "❌ System health check failed - aborting tests" -ForegroundColor $Colors.Error
        exit 1
    }
    
    # Phase 2: Frame Detection
    $frameResult = Test-Frames
    
    # Phase 3: Keybindings (with auto-fix retry loop)
    $keybindResult = $null
    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        $keybindResult = Test-Keybindings
        if ($keybindResult.Pass) {
            break
        }
        
        if ($attempt -lt $MaxRetries) {
            Write-Host ""
            Write-Host "  🔄 Attempt $attempt/$MaxRetries - Applying keybind fixes..." -ForegroundColor $Colors.Warning
            $fixResult = Fix-Keybindings
            Write-Host "  ⏳ Waiting $Script:WaitBetweenFixes seconds for changes to apply..." -ForegroundColor $Colors.Metric
            Start-Sleep -Seconds $Script:WaitBetweenFixes
        }
    }
    
    # Phase 4: Action Bar (with auto-fix retry loop)
    $actionBarResult = $null
    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        $actionBarResult = Test-ActionBar
        if ($actionBarResult.Pass) {
            break
        }
        
        if ($attempt -lt $MaxRetries) {
            Write-Host ""
            Write-Host "  🔄 Attempt $attempt/$MaxRetries - Syncing action bar..." -ForegroundColor $Colors.Warning
            $fixResult = Fix-ActionBar
            Write-Host "  ⏳ Waiting $Script:WaitBetweenFixes seconds for changes to apply..." -ForegroundColor $Colors.Metric
            Start-Sleep -Seconds $Script:WaitBetweenFixes
        }
    }
    
    # Phase 5: Combat Tests (if prerequisites passed and not skipped)
    if ($keybindResult.Pass -and $actionBarResult.Pass -and -not $SkipCombatTests) {
        $combatResult = Test-CombatBasic
    }
    elseif ($SkipCombatTests) {
        Write-Host ""
        Write-Host "  ⏭️  Skipping combat tests (--SkipCombatTests)" -ForegroundColor $Colors.Warning
    }
    
    # Phase 6: Bot Cycle Test
    $botResult = Test-BotCycle -DurationSeconds $BotCycleDuration
    
    # Generate Summary
    $Script:Report.Summary = @{
        TotalPhases = $Script:Report.Phases.Count
        Passed = ($Script:Report.Phases | Where-Object { $_.Pass }).Count
        Failed = ($Script:Report.Phases | Where-Object { -not $_.Pass }).Count
        BotWorking = $botResult.Pass
        Duration = $BotCycleDuration
        Metrics = $botResult.Metrics
    }
    
    # Save Report
    $reportPath = "test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $Script:Report | ConvertTo-Json -Depth 10 | Out-File $reportPath -Encoding UTF8
    
    # Final Summary
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
    Write-Host "  📊 TEST SUMMARY" -ForegroundColor $Colors.Header
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
    Write-Host "  Phases Passed: $($Script:Report.Summary.Passed)/$($Script:Report.Summary.TotalPhases)" -ForegroundColor $(if ($Script:Report.Summary.Passed -eq $Script:Report.Summary.TotalPhases) { $Colors.Success } else { $Colors.Warning })
    Write-Host "  Bot Working: $(if ($botResult.Pass) { '✅ YES' } else { '❌ NO' })" -ForegroundColor $(if ($botResult.Pass) { $Colors.Success } else { $Colors.Error })
    
    if ($botResult.Metrics) {
        Write-Host "  Distance Moved: $($botResult.Metrics.DistanceMoved) units" -ForegroundColor $Colors.Metric
        Write-Host "  Combat Entered: $(if ($botResult.Metrics.CombatEntered) { 'Yes' } else { 'No' })" -ForegroundColor $Colors.Metric
        Write-Host "  Estimated Kills: $($botResult.Metrics.EstimatedKills)" -ForegroundColor $Colors.Metric
    }
    
    Write-Host "  Report Saved: $reportPath" -ForegroundColor $Colors.Info
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Header
    
    # Exit with appropriate code
    exit $(if ($botResult.Pass) { 0 } else { 1 })
}
catch {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Error
    Write-Host "  ❌ FATAL ERROR" -ForegroundColor $Colors.Error
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Error
    Write-Host "  $_" -ForegroundColor $Colors.Error
    Write-Host "  $($_.ScriptStackTrace)" -ForegroundColor $Colors.Metric
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor $Colors.Error
    
    exit 2
}

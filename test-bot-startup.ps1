# WowClassicGrindBot - Automated Test Suite
# Requires: BlazorServer running on localhost:5000

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$global:TestsPassed = 0
$global:TestsFailed = 0
$global:Warnings = @()

function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n================================" -ForegroundColor Cyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "================================`n" -ForegroundColor Cyan
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Details = ""
    )
    
    if ($Passed) {
        Write-Host "[PASS] $TestName" -ForegroundColor Green
        $global:TestsPassed++
    } else {
        Write-Host "[FAIL] $TestName" -ForegroundColor Red
        if ($Details) {
            Write-Host "       Details: $Details" -ForegroundColor Yellow
        }
        $global:TestsFailed++
    }
}

function Write-TestWarning {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
    $global:Warnings += $Message
}

function Test-ApiEndpoint {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [int]$ExpectedStatusCode = 200
    )
    
    try {
        $response = Invoke-WebRequest -Uri "$BaseUrl$Endpoint" -Method $Method -UseBasicParsing -TimeoutSec 10
        return @{
            Success = ($response.StatusCode -eq $ExpectedStatusCode)
            StatusCode = $response.StatusCode
            Content = $response.Content
            Error = $null
        }
    } catch {
        return @{
            Success = $false
            StatusCode = 0
            Content = $null
            Error = $_.Exception.Message
        }
    }
}

# =============================================================================
# TEST 1: Server Availability
# =============================================================================
Write-TestHeader "Test 1: Server Availability"

$pingResult = Test-ApiEndpoint -Endpoint "/" -ExpectedStatusCode 200
Write-TestResult "BlazorServer is running" $pingResult.Success $pingResult.Error

if (-not $pingResult.Success) {
    Write-Host "`nERROR: BlazorServer is not accessible at $BaseUrl" -ForegroundColor Red
    Write-Host "Please start the server with: cd BlazorServer && dotnet run -c Debug" -ForegroundColor Yellow
    exit 1
}

# =============================================================================
# TEST 2: Frame Detection
# =============================================================================
Write-TestHeader "Test 2: Frame Detection"

$framesResult = Test-ApiEndpoint -Endpoint "/api/test/frames"
if ($framesResult.Success) {
    try {
        $framesData = $framesResult.Content | ConvertFrom-Json
        
        if ($null -eq $framesData.totalFrames) {
            Write-TestWarning "Frames API returned unexpected format"
            Write-TestResult "Frame detection total" $false "Expected totalFrames field"
        } else {
            Write-Host "  Total frames: $($framesData.totalFrames)" -ForegroundColor Gray
            Write-Host "  Detected: $($framesData.detectedFrames)" -ForegroundColor Gray
            
            $allFramesDetected = ($framesData.totalFrames -eq 324) -and ($framesData.detectedFrames -eq 324)
            Write-TestResult "All 324 frames detected" $allFramesDetected
            
            if ($framesData.missingFrames -and $framesData.missingFrames.Count -gt 0) {
                Write-TestWarning "Missing frames: $($framesData.missingFrames -join ', ')"
            }
        }
    } catch {
        Write-TestResult "Frame data parsing" $false $_.Exception.Message
    }
} else {
    Write-TestResult "Frame detection API" $false $framesResult.Error
}

# =============================================================================
# TEST 3: System Status
# =============================================================================
Write-TestHeader "Test 3: System Status"

$statusResult = Test-ApiEndpoint -Endpoint "/api/test/status"
if ($statusResult.Success) {
    try {
        $statusData = $statusResult.Content | ConvertFrom-Json
        
        if ($statusData.wowProcessFound) {
            Write-Host "  WoW Process: Found (PID: $($statusData.processId))" -ForegroundColor Gray
            Write-TestResult "WoW process detected" $true
        } else {
            Write-TestWarning "WoW process not found - start WoW and log in with a character"
            Write-TestResult "WoW process detected" $false
        }
        
        if ($statusData.addonLoaded) {
            Write-Host "  DataToColor addon: Loaded" -ForegroundColor Gray
            Write-TestResult "DataToColor addon loaded" $true
        } else {
            Write-TestWarning "DataToColor addon not detected"
            Write-TestResult "DataToColor addon loaded" $false
        }
        
    } catch {
        Write-TestResult "Status data parsing" $false $_.Exception.Message
    }
} else {
    Write-TestResult "System status API" $false $statusResult.Error
}

# =============================================================================
# TEST 4: Player Snapshot
# =============================================================================
Write-TestHeader "Test 4: Player Snapshot"

$snapshotResult = Test-ApiEndpoint -Endpoint "/api/test/snapshot"
if ($snapshotResult.Success) {
    try {
        $playerData = $snapshotResult.Content | ConvertFrom-Json
        
        if ($playerData.playerName) {
            Write-Host "  Character: $($playerData.playerName)" -ForegroundColor Gray
            Write-Host "  Level: $($playerData.level)" -ForegroundColor Gray
            Write-Host "  Class: $($playerData.class)" -ForegroundColor Gray
            Write-Host "  Zone: $($playerData.zone)" -ForegroundColor Gray
            Write-Host "  Health: $($playerData.healthCurrent)/$($playerData.healthMax)" -ForegroundColor Gray
            
            Write-TestResult "Player name retrieved" ($null -ne $playerData.playerName)
            Write-TestResult "Player level valid" ($playerData.level -gt 0)
            Write-TestResult "Player class valid" ($null -ne $playerData.class)
            Write-TestResult "Health data valid" ($playerData.healthMax -gt 0)
            
        } else {
            Write-TestWarning "Player data not available - ensure character is logged in"
            Write-TestResult "Player snapshot" $false "No player data"
        }
        
    } catch {
        Write-TestResult "Snapshot data parsing" $false $_.Exception.Message
    }
} else {
    Write-TestResult "Player snapshot API" $false $snapshotResult.Error
}

# =============================================================================
# TEST 5: Input System
# =============================================================================
Write-TestHeader "Test 5: Input System Test"

Write-Host "Testing input simulation (character will jump if WoW is focused)..." -ForegroundColor Gray

$jumpResult = Test-ApiEndpoint -Endpoint "/api/test/input/jump" -Method "POST"
Write-TestResult "Jump command sent" $jumpResult.Success $jumpResult.Error

Start-Sleep -Milliseconds 500

# =============================================================================
# TEST 6: Event Log Check for Crashes
# =============================================================================
Write-TestHeader "Test 6: Recent Crash Check"

Write-Host "Checking Windows Event Log for recent crashes..." -ForegroundColor Gray

try {
    $recentCrashes = Get-WinEvent -FilterHashtable @{
        LogName = 'Application'
        ProviderName = '.NET Runtime'
        Level = 2  # Error
        StartTime = (Get-Date).AddHours(-1)
    } -MaxEvents 5 -ErrorAction SilentlyContinue | Where-Object {
        $_.Message -like "*BlazorServer*" -or $_.Message -like "*ActionBarCooldownReader*"
    }
    
    if ($recentCrashes) {
        Write-TestWarning "Found $($recentCrashes.Count) recent crash(es) in Event Log"
        $recentCrashes | ForEach-Object {
            Write-Host "  - $($_.TimeCreated): $($_.Message.Substring(0, [Math]::Min(100, $_.Message.Length)))..." -ForegroundColor Yellow
        }
        Write-TestResult "No recent crashes" $false "Found crashes in last hour"
    } else {
        Write-TestResult "No recent crashes" $true
    }
} catch {
    Write-TestWarning "Could not check Event Log (may need admin privileges)"
}

# =============================================================================
# TEST 7: Log File Analysis
# =============================================================================
Write-TestHeader "Test 7: Log File Analysis"

$logPath = Join-Path $PSScriptRoot "BlazorServer\bin\Debug\net10.0\logs"
if (Test-Path $logPath) {
    $latestLog = Get-ChildItem $logPath -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    
    if ($latestLog) {
        Write-Host "  Analyzing: $($latestLog.Name)" -ForegroundColor Gray
        
        $logContent = Get-Content $latestLog.FullName -Tail 500
        
        # Check for errors
        $errors = $logContent | Where-Object { $_ -like "*[ERROR]*" -or $_ -like "*Exception*" }
        if ($errors) {
            Write-TestWarning "Found $($errors.Count) error(s) in recent logs"
            Write-TestResult "No errors in logs" $false "Review log file for details"
        } else {
            Write-TestResult "No errors in logs" $true
        }
        
        # Check for bounds warnings
        $boundsWarnings = $logContent | Where-Object { $_ -like "*Invalid slot index*" }
        if ($boundsWarnings) {
            Write-TestWarning "Found $($boundsWarnings.Count) invalid slot index warning(s)"
            $boundsWarnings | Select-Object -First 3 | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Yellow
            }
        } else {
            Write-TestResult "No bounds warnings" $true
        }
        
        # Check for cooldown updates (DEBUG mode)
        $cooldownUpdates = $logContent | Where-Object { $_ -like "*ActionBarCooldownReader*cooldown*" }
        if ($cooldownUpdates) {
            Write-Host "  Found $($cooldownUpdates.Count) cooldown update(s) (DEBUG logging working)" -ForegroundColor Gray
        }
        
    } else {
        Write-TestWarning "No log files found in $logPath"
    }
} else {
    Write-TestWarning "Log directory not found: $logPath"
}

# =============================================================================
# TEST SUMMARY
# =============================================================================
Write-Host "`n" -NoNewline
Write-Host "================================" -ForegroundColor Cyan
Write-Host " TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

Write-Host "Total Tests Run:  " -NoNewline
Write-Host ($global:TestsPassed + $global:TestsFailed) -ForegroundColor White

Write-Host "Tests Passed:     " -NoNewline
Write-Host $global:TestsPassed -ForegroundColor Green

Write-Host "Tests Failed:     " -NoNewline
Write-Host $global:TestsFailed -ForegroundColor $(if ($global:TestsFailed -eq 0) { "Green" } else { "Red" })

if ($global:Warnings.Count -gt 0) {
    Write-Host "Warnings:         " -NoNewline
    Write-Host $global:Warnings.Count -ForegroundColor Yellow
}

Write-Host "`nTest Result: " -NoNewline
if ($global:TestsFailed -eq 0) {
    Write-Host "SUCCESS" -ForegroundColor Green
    $exitCode = 0
} else {
    Write-Host "FAILURE" -ForegroundColor Red
    Write-Host "`nReview failed tests above and check logs for details." -ForegroundColor Yellow
    $exitCode = 1
}

Write-Host "================================`n" -ForegroundColor Cyan

# Generate test report
$reportPath = Join-Path $PSScriptRoot "test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
@"
WowClassicGrindBot Test Report
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
================================

Tests Passed: $global:TestsPassed
Tests Failed: $global:TestsFailed
Warnings: $($global:Warnings.Count)

Result: $(if ($global:TestsFailed -eq 0) { 'SUCCESS' } else { 'FAILURE' })

Warnings:
$($global:Warnings -join "`n")

================================
"@ | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "Test report saved to: $reportPath`n" -ForegroundColor Gray

exit $exitCode

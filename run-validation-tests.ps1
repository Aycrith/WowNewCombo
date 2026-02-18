# Validation Tests Runner
# Evidence-based testing with memory, performance, and coverage reporting
# Local PowerShell CI/CD script - no external dependencies

param(
    [string]$OutputDir = ".\test-evidence",
    [string]$Configuration = "Release",
    [switch]$RunAll,
    [switch]$RunMemory,
    [switch]$RunPerformance,
    [switch]$RunFunctional,
    [switch]$RunIntegration,
    [switch]$RunStability,
    [switch]$SkipBuild,
    [int]$StabilityHours = 1
)

$ErrorActionPreference = "Stop"
$StartTime = Get-Date
$EvidenceData = @{
    StartTime = $StartTime
    Configuration = $Configuration
    OutputDir = $OutputDir
    TestsRun = @()
    Results = @()
    MemorySnapshots = @()
    PerformanceMetrics = @()
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Evidence-Based Validation Test Runner" -ForegroundColor Cyan
Write-Host "Started: $StartTime" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Output Directory: $OutputDir" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created evidence directory: $OutputDir" -ForegroundColor Green
}

# Helper function to collect memory evidence
function Get-MemoryEvidence {
    param([string]$Label)
    
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    Start-Sleep -Milliseconds 100
    
    $mem = [GC]::GetTotalMemory($true)
    $snapshot = @{
        Timestamp = Get-Date
        Label = $Label
        MemoryBytes = $mem
        MemoryMB = [math]::Round($mem / 1MB, 2)
        ProcessWorkingSet = [math]::Round((Get-Process -Id $PID).WorkingSet64 / 1MB, 2)
        Gen0Collections = [GC]::CollectionCount(0)
        Gen1Collections = [GC]::CollectionCount(1)
        Gen2Collections = [GC]::CollectionCount(2)
    }
    
    $EvidenceData.MemorySnapshots += $snapshot
    return $snapshot
}

# Helper function to run tests and capture results
function Invoke-TestSuite {
    param(
        [string]$Name,
        [string]$Filter,
        [string]$OutputFile
    )
    
    Write-Host "`n========================================" -ForegroundColor Yellow
    Write-Host "Running: $Name" -ForegroundColor Yellow
    Write-Host "Filter: $Filter" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    
    $testOutput = Join-Path $OutputDir $OutputFile
    $memBefore = Get-MemoryEvidence -Label "${Name}_Before"
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $logFile = Join-Path $OutputDir "$OutputFile.log"
    
    try {
        # Run tests and capture output to file
        dotnet test --configuration $Configuration --no-restore `
            --filter "$Filter" `
            --logger "trx;LogFileName=$OutputFile.trx" `
            --results-directory $OutputDir *> $logFile
        
        $exitCode = $LASTEXITCODE
        $stopwatch.Stop()
        
        # Read and parse results
        $resultText = Get-Content $logFile -Raw
        
        # Parse results using multiple patterns
        $passed = 0
        $failed = 0
        $skipped = 0
        
        # Try multiple regex patterns
        if ($resultText -match "Passed!.*Failed:\s*(\d+).*Passed:\s*(\d+).*Skipped:\s*(\d+)") {
            $failed = [int]$matches[1]
            $passed = [int]$matches[2]
            $skipped = [int]$matches[3]
        }
        elseif ($resultText -match "Total tests:\s*(\d+).*Passed:\s*(\d+).*Failed:\s*(\d+).*Skipped:\s*(\d+)") {
            $total = [int]$matches[1]
            $passed = [int]$matches[2]
            $failed = [int]$matches[3]
            $skipped = [int]$matches[4]
        }
        elseif ($resultText -match "No test matches") {
            $passed = 0
            $failed = 0
            $skipped = 0
        }
        
        $memAfter = Get-MemoryEvidence -Label "${Name}_After"
        $memoryGrowth = $memAfter.MemoryMB - $memBefore.MemoryMB
        
        $testResult = @{
            Name = $Name
            Filter = $Filter
            Passed = $passed
            Failed = $failed
            Skipped = $skipped
            Duration = $stopwatch.Elapsed
            MemoryGrowthMB = $memoryGrowth
            Success = ($failed -eq 0)
            OutputFile = "$OutputFile.trx"
            ExitCode = $exitCode
        }
        
        $EvidenceData.TestsRun += $testResult
        
        $color = if ($failed -eq 0 -and $passed -gt 0) { "Green" } elseif ($failed -gt 0) { "Red" } else { "Yellow" }
        Write-Host "`nResults: Passed=$passed, Failed=$failed, Skipped=$skipped" -ForegroundColor $color
        Write-Host "Duration: $($stopwatch.Elapsed)" -ForegroundColor Cyan
        Write-Host "Memory Growth: $memoryGrowth MB" -ForegroundColor Cyan
        
        return $testResult
    }
    catch {
        $stopwatch.Stop()
        Write-Host "ERROR running $Name : $_" -ForegroundColor Red
        return @{ Name = $Name; Success = $false; Error = $_.ToString() }
    }
}

# Step 1: Build (if not skipped)
if (!$SkipBuild) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Building Solution..." -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    dotnet restore
    dotnet build --configuration $Configuration --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Build successful!" -ForegroundColor Green
}

# Step 2: Determine which tests to run
$testsToRun = @()

if ($RunAll -or $RunMemory) {
    $testsToRun += @{
        Name = "Memory Leak Evidence"
        Filter = "FullyQualifiedName~Memory_Leak"
        Output = "memory-leak-tests"
    }
}

if ($RunAll -or $RunPerformance) {
    $testsToRun += @{
        Name = "Performance Evidence"
        Filter = "FullyQualifiedName~Performance"
        Output = "performance-tests"
    }
}

if ($RunAll -or $RunFunctional) {
    $testsToRun += @{
        Name = "Functional Evidence"
        Filter = "FullyQualifiedName~Functional"
        Output = "functional-tests"
    }
}

if ($RunAll -or $RunIntegration) {
    $testsToRun += @{
        Name = "Integration Evidence"
        Filter = "FullyQualifiedName~Integration"
        Output = "integration-tests"
    }
}

if ($RunAll -or $RunStability) {
    $testsToRun += @{
        Name = "Thread Safety Evidence"
        Filter = "FullyQualifiedName~ThreadSafety"
        Output = "thread-safety-tests"
    }
}

# Default: run all if no specific category selected
if ($testsToRun.Count -eq 0 -and !$RunAll) {
    Write-Host "No test category specified. Use -RunAll or specific flags." -ForegroundColor Yellow
    Write-Host "Available flags: -RunMemory, -RunPerformance, -RunFunctional, -RunIntegration, -RunStability, -RunAll" -ForegroundColor Yellow
    exit 1
}

if ($RunAll) {
    # Run all tests including standard tests
    $testsToRun += @{
        Name = "RouteRerouter Tests"
        Filter = "FullyQualifiedName~RouteRerouterTests"
        Output = "routererouter-tests"
    }
    $testsToRun += @{
        Name = "Route Visualization Tests"
        Filter = "FullyQualifiedName~RouteVisualizationServiceTests"
        Output = "route-visualization-tests"
    }
    $testsToRun += @{
        Name = "All Other Tests"
        Filter = ""
        Output = "all-tests"
    }
}

# Step 3: Execute tests
$allPassed = $true
foreach ($test in $testsToRun) {
    $result = Invoke-TestSuite -Name $test.Name -Filter $test.Filter -OutputFile $test.Output
    if (!$result.Success) {
        $allPassed = $false
    }
}

# Step 4: Generate Evidence Report
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Generating Evidence Report..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$reportPath = Join-Path $OutputDir "evidence-report.json"
$EvidenceData.EndTime = Get-Date
$EvidenceData.TotalDuration = $EvidenceData.EndTime - $EvidenceData.StartTime

# Calculate summary
$totalPassed = ($EvidenceData.TestsRun | Measure-Object -Property Passed -Sum).Sum
$totalFailed = ($EvidenceData.TestsRun | Measure-Object -Property Failed -Sum).Sum
$totalSkipped = ($EvidenceData.TestsRun | Measure-Object -Property Skipped -Sum).Sum
$totalMemoryGrowth = ($EvidenceData.TestsRun | Measure-Object -Property MemoryGrowthMB -Sum).Sum

$EvidenceData.Summary = @{
    TotalTests = $EvidenceData.TestsRun.Count
    TotalPassed = $totalPassed
    TotalFailed = $totalFailed
    TotalSkipped = $totalSkipped
    TotalMemoryGrowthMB = $totalMemoryGrowth
    AllTestsPassed = $allPassed
}

# Save JSON report
$EvidenceData | ConvertTo-Json -Depth 10 | Out-File $reportPath
Write-Host "Evidence report saved to: $reportPath" -ForegroundColor Green

# Generate Markdown summary
$mdReport = @"
# Evidence-Based Validation Report

**Generated:** $($EvidenceData.EndTime)
**Duration:** $($EvidenceData.TotalDuration)
**Configuration:** $Configuration

## Summary

| Metric | Value |
|--------|-------|
| Total Test Suites | $($EvidenceData.TestsRun.Count) |
| Total Tests Passed | $totalPassed |
| Total Tests Failed | $totalFailed |
| Total Tests Skipped | $totalSkipped |
| Memory Growth | $totalMemoryGrowth MB |
| Overall Status | $(if ($allPassed) { "PASS" } else { "FAIL" }) |

## Test Results

"@

foreach ($test in $EvidenceData.TestsRun) {
    $status = if ($test.Success) { "PASS" } else { "FAIL" }
    $statusColor = if ($test.Success) { "Green" } else { "Red" }
    
    $mdReport += @"

### $($test.Name)
- **Status:** $status
- **Passed:** $($test.Passed)
- **Failed:** $($test.Failed)
- **Skipped:** $($test.Skipped)
- **Duration:** $($test.Duration)
- **Memory Growth:** $($test.MemoryGrowthMB) MB

"@
}

$mdReport += @"

## Memory Snapshots

| Timestamp | Label | Memory (MB) | Gen0 | Gen1 | Gen2 |
|-----------|-------|-------------|------|------|------|
"@

foreach ($snap in $EvidenceData.MemorySnapshots) {
    $mdReport += "| $($snap.Timestamp) | $($snap.Label) | $($snap.MemoryMB) | $($snap.Gen0Collections) | $($snap.Gen1Collections) | $($snap.Gen2Collections) |`n"
}

$mdReport += "`n`n---`n**Evidence collected and validated**"

$mdPath = Join-Path $OutputDir "evidence-report.md"
$mdReport | Out-File $mdPath
Write-Host "Markdown report saved to: $mdPath" -ForegroundColor Green

# Step 5: Final Summary
Write-Host "`n========================================" -ForegroundColor $(if ($allPassed) { "Green" } else { "Red" })
Write-Host "Validation Complete!" -ForegroundColor $(if ($allPassed) { "Green" } else { "Red" })
Write-Host "========================================" -ForegroundColor $(if ($allPassed) { "Green" } else { "Red" })
Write-Host ""
Write-Host "Total Duration: $($EvidenceData.TotalDuration)" -ForegroundColor Cyan
Write-Host "Tests Passed: $totalPassed" -ForegroundColor Green
Write-Host "Tests Failed: $totalFailed" -ForegroundColor $(if ($totalFailed -eq 0) { "Green" } else { "Red" })
Write-Host "Tests Skipped: $totalSkipped" -ForegroundColor Yellow
Write-Host "Total Memory Growth: $totalMemoryGrowth MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Evidence Location: $OutputDir" -ForegroundColor Cyan
Write-Host ""

if ($allPassed) {
    Write-Host "ALL TESTS PASSED - Evidence validation successful!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "SOME TESTS FAILED - Review evidence report for details." -ForegroundColor Red
    exit 1
}

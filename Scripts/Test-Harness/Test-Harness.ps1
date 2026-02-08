#Requires -Version 7.0
<#
.SYNOPSIS
    Comprehensive Test Harness for WowClassicGrindBot

.DESCRIPTION
    Automated test pipeline that runs all end-to-end scenarios
    using the MockWoWClient synthetic testing environment.

.PARAMETER Stages
    Which test stages to run. Options: All, PreFlight, Unit, Integration, E2E, Performance

.PARAMETER Scenarios
    Specific E2E scenarios to run. If empty, runs all scenarios.

.PARAMETER OutputPath
    Directory for test reports.

.PARAMETER Parallel
    Run tests in parallel where possible.

.PARAMETER ReportOnly
    Only generate reports from previous test results.

.EXAMPLE
    .\Test-Harness.ps1 -Stages All
    Runs all test stages.

.EXAMPLE
    .\Test-Harness.ps1 -Stages E2E -Scenarios "BotStartup,CombatRotation"
    Runs only specified E2E scenarios.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("All", "PreFlight", "Unit", "Integration", "E2E", "Performance")]
    [string[]]$Stages = @("All"),
    
    [Parameter()]
    [string[]]$Scenarios = @(),
    
    [Parameter()]
    [string]$OutputPath = "./TestResults",
    
    [Parameter()]
    [switch]$Parallel,
    
    [Parameter()]
    [switch]$ReportOnly
)

# Configuration
$ErrorActionPreference = "Stop"
$ProgressPreference = "Continue"

# Test results collection
$script:TestResults = @()
$script:StartTime = Get-Date

# =============================================================================
# HEADER
# =============================================================================
function Write-Header {
    param([string]$Message)
    Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║ $Message" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
}

function Write-Section {
    param([string]$Message)
    Write-Host "`n─────────────────────────────────────────────────────────────────" -ForegroundColor Yellow
    Write-Host " $Message" -ForegroundColor Yellow
    Write-Host "─────────────────────────────────────────────────────────────────`n" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "  ✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "  ℹ $Message" -ForegroundColor Gray
}

# =============================================================================
# STAGE 1: PRE-FLIGHT CHECKS
# =============================================================================
function Invoke-PreFlightStage {
    Write-Section "Stage 1: Pre-Flight Checks"
    
    $results = @()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    # Check 1: .NET SDK
    Write-Info "Checking .NET SDK..."
    try {
        $dotnetVersion = dotnet --version
        Write-Success ".NET SDK found: $dotnetVersion"
        $results += [PSCustomObject]@{ Name = "DotNetSDK"; Status = "PASS"; Details = $dotnetVersion }
    }
    catch {
        Write-Failure ".NET SDK not found"
        $results += [PSCustomObject]@{ Name = "DotNetSDK"; Status = "FAIL"; Details = $_.Exception.Message }
        return $results
    }
    
    # Check 2: Solution builds
    Write-Info "Building solution..."
    try {
        $buildOutput = dotnet build MasterOfPuppets.sln -c Release 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Solution builds successfully"
            $results += [PSCustomObject]@{ Name = "Build"; Status = "PASS"; Details = "Release build successful" }
        }
        else {
            Write-Failure "Build failed"
            $results += [PSCustomObject]@{ Name = "Build"; Status = "FAIL"; Details = "See build log" }
        }
    }
    catch {
        Write-Failure "Build error: $($_.Exception.Message)"
        $results += [PSCustomObject]@{ Name = "Build"; Status = "FAIL"; Details = $_.Exception.Message }
    }
    
    # Check 3: Unit tests compile
    Write-Info "Checking test projects..."
    try {
        $testProjects = @(
            "CoreUnitTests\CoreUnitTests.csproj"
        )
        
        $allCompile = $true
        foreach ($project in $testProjects) {
            $compileOutput = dotnet build $project --no-restore 2>&1
            if ($LASTEXITCODE -ne 0) {
                $allCompile = $false
                break
            }
        }
        
        if ($allCompile) {
            Write-Success "All test projects compile"
            $results += [PSCustomObject]@{ Name = "TestCompilation"; Status = "PASS"; Details = "All projects compile" }
        }
        else {
            Write-Failure "Some test projects failed to compile"
            $results += [PSCustomObject]@{ Name = "TestCompilation"; Status = "FAIL"; Details = "Compilation errors" }
        }
    }
    catch {
        Write-Failure "Test compilation error: $($_.Exception.Message)"
        $results += [PSCustomObject]@{ Name = "TestCompilation"; Status = "FAIL"; Details = $_.Exception.Message }
    }
    
    $stopwatch.Stop()
    Write-Info "Pre-flight checks completed in $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
    
    return $results
}

# =============================================================================
# STAGE 2: UNIT TESTS
# =============================================================================
function Invoke-UnitTestStage {
    Write-Section "Stage 2: Unit Tests"
    
    $results = @()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    Write-Info "Running CoreUnitTests..."
    try {
        $testOutput = dotnet test CoreUnitTests\CoreUnitTests.csproj --no-build --logger "trx" --results-directory $OutputPath 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "All unit tests passed"
            $results += [PSCustomObject]@{ Name = "UnitTests"; Status = "PASS"; Details = "All tests passed" }
        }
        else {
            Write-Failure "Some unit tests failed"
            $results += [PSCustomObject]@{ Name = "UnitTests"; Status = "FAIL"; Details = "See test results" }
        }
    }
    catch {
        Write-Failure "Unit test execution failed: $($_.Exception.Message)"
        $results += [PSCustomObject]@{ Name = "UnitTests"; Status = "FAIL"; Details = $_.Exception.Message }
    }
    
    $stopwatch.Stop()
    Write-Info "Unit tests completed in $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
    
    return $results
}

# =============================================================================
# STAGE 3: E2E SCENARIOS
# =============================================================================
function Invoke-E2EStage {
    param([string[]]$ScenarioFilter)
    
    Write-Section "Stage 3: End-to-End Scenarios"
    
    $results = @()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    # Build filter expression
    $filter = "Category=E2E"
    if ($ScenarioFilter.Count -gt 0) {
        $scenarioFilters = $ScenarioFilter | ForEach-Object { "Scenario=$_" }
        $filter = ($scenarioFilters -join "|")
    }
    
    Write-Info "Running E2E scenarios with filter: $filter"
    
    try {
        $testOutput = dotnet test CoreUnitTests\CoreUnitTests.csproj `
            --filter "$filter" `
            --no-build `
            --logger "trx" `
            --results-directory "$OutputPath\E2E" `
            2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "All E2E scenarios passed"
            $results += [PSCustomObject]@{ Name = "E2EScenarios"; Status = "PASS"; Details = "All scenarios passed" }
        }
        else {
            Write-Failure "Some E2E scenarios failed"
            $results += [PSCustomObject]@{ Name = "E2EScenarios"; Status = "FAIL"; Details = "See test results" }
        }
    }
    catch {
        Write-Failure "E2E test execution failed: $($_.Exception.Message)"
        $results += [PSCustomObject]@{ Name = "E2EScenarios"; Status = "FAIL"; Details = $_.Exception.Message }
    }
    
    $stopwatch.Stop()
    Write-Info "E2E scenarios completed in $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
    
    return $results
}

# =============================================================================
# STAGE 4: PERFORMANCE TESTS
# =============================================================================
function Invoke-PerformanceStage {
    Write-Section "Stage 4: Performance Tests"
    
    $results = @()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    Write-Info "Running performance benchmarks..."
    
    # Run long-running memory leak test
    try {
        $testOutput = dotnet test CoreUnitTests\CoreUnitTests.csproj `
            --filter "Category=LongRunning" `
            --no-build `
            --logger "trx" `
            --results-directory "$OutputPath\Performance" `
            2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Performance tests passed"
            $results += [PSCustomObject]@{ Name = "Performance"; Status = "PASS"; Details = "Performance tests passed" }
        }
        else {
            Write-Failure "Some performance tests failed"
            $results += [PSCustomObject]@{ Name = "Performance"; Status = "FAIL"; Details = "See test results" }
        }
    }
    catch {
        Write-Failure "Performance test execution failed: $($_.Exception.Message)"
        $results += [PSCustomObject]@{ Name = "Performance"; Status = "FAIL"; Details = $_.Exception.Message }
    }
    
    $stopwatch.Stop()
    Write-Info "Performance tests completed in $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
    
    return $results
}

# =============================================================================
# STAGE 5: REPORT GENERATION
# =============================================================================
function Invoke-ReportStage {
    Write-Section "Stage 5: Report Generation"
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    # Create output directory
    if (-not (Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $reportDir = "$OutputPath\$timestamp"
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    
    # Generate summary report
    $summary = @{
        Timestamp = Get-Date -Format "o"
        Duration = ([DateTime]::Now - $script:StartTime).ToString()
        Results = $script:TestResults
        TotalTests = $script:TestResults.Count
        PassedTests = ($script:TestResults | Where-Object { $_.Status -eq "PASS" }).Count
        FailedTests = ($script:TestResults | Where-Object { $_.Status -eq "FAIL" }).Count
    }
    
    # JSON report
    $summary | ConvertTo-Json -Depth 10 | Out-File "$reportDir\results.json"
    Write-Info "JSON report saved to: $reportDir\results.json"
    
    # Markdown report
    $markdown = @"
# Test Report - $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Summary

- **Total Tests**: $($summary.TotalTests)
- **Passed**: $($summary.PassedTests) ✅
- **Failed**: $($summary.FailedTests) $($summary.FailedTests -gt 0 ? '❌' : '')
- **Duration**: $($summary.Duration)

## Results by Stage

| Stage | Status | Details |
|-------|--------|---------|
"@
    
    foreach ($result in $script:TestResults) {
        $statusIcon = if ($result.Status -eq "PASS") { "✅" } else { "❌" }
        $markdown += "\n| $($result.Name) | $statusIcon $($result.Status) | $($result.Details) |"
    }
    
    $markdown += "\n\n## Generated Files\n\n"
    $markdown += "- JSON Results: `results.json`\n"
    $markdown += "- TRX Results: See `E2E\` and `Performance\` directories\n"
    $markdown += "\n---\n*Generated by Test-Harness.ps1*\n"
    
    $markdown | Out-File "$reportDir\summary.md"
    Write-Info "Markdown report saved to: $reportDir\summary.md"
    
    # HTML report (simple)
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Test Report - $timestamp</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .header { background: #f0f0f0; padding: 20px; border-radius: 5px; }
        .summary { margin: 20px 0; }
        .pass { color: green; }
        .fail { color: red; }
        table { border-collapse: collapse; width: 100%; margin: 20px 0; }
        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }
        th { background-color: #4CAF50; color: white; }
        tr:nth-child(even) { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <div class="header">
        <h1>Test Report</h1>
        <p><strong>Timestamp:</strong> $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
        <p><strong>Duration:</strong> $($summary.Duration)</p>
    </div>
    
    <div class="summary">
        <h2>Summary</h2>
        <p>Total Tests: <strong>$($summary.TotalTests)</strong></p>
        <p class="pass">Passed: $($summary.PassedTests) ✅</p>
        <p class="fail">Failed: $($summary.FailedTests)</p>
    </div>
    
    <h2>Detailed Results</h2>
    <table>
        <tr>
            <th>Test</th>
            <th>Status</th>
            <th>Details</th>
        </tr>
"@
    
    foreach ($result in $script:TestResults) {
        $rowClass = if ($result.Status -eq "PASS") { "pass" } else { "fail" }
        $html += "        <tr class=`"$rowClass`"><td>$($result.Name)</td><td>$($result.Status)</td><td>$($result.Details)</td></tr>\n"
    }
    
    $html += @"
    </table>
</body>
</html>
"@
    
    $html | Out-File "$reportDir\report.html"
    Write-Info "HTML report saved to: $reportDir\report.html"
    
    $stopwatch.Stop()
    Write-Info "Report generation completed in $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
    
    return $reportDir
}

# =============================================================================
# MAIN EXECUTION
# =============================================================================
Write-Header "WowClassicGrindBot Test Harness"
Write-Info "Starting comprehensive test pipeline..."
Write-Info "Output directory: $OutputPath"

# Determine which stages to run
$runPreFlight = $Stages -contains "All" -or $Stages -contains "PreFlight"
$runUnit = $Stages -contains "All" -or $Stages -contains "Unit"
$runE2E = $Stages -contains "All" -or $Stages -contains "E2E"
$runPerformance = $Stages -contains "All" -or $Stages -contains "Performance"

# Run stages
if ($ReportOnly) {
    Write-Info "Report-only mode - skipping test execution"
}
else {
    if ($runPreFlight) {
        $preFlightResults = Invoke-PreFlightStage
        $script:TestResults += $preFlightResults
    }
    
    if ($runUnit) {
        $unitResults = Invoke-UnitTestStage
        $script:TestResults += $unitResults
    }
    
    if ($runE2E) {
        $e2eResults = Invoke-E2EStage -ScenarioFilter $Scenarios
        $script:TestResults += $e2eResults
    }
    
    if ($runPerformance) {
        $perfResults = Invoke-PerformanceStage
        $script:TestResults += $perfResults
    }
}

# Generate reports
$reportPath = Invoke-ReportStage

# Final summary
$totalDuration = [DateTime]::Now - $script:StartTime
$passedCount = ($script:TestResults | Where-Object { $_.Status -eq "PASS" }).Count
$failedCount = ($script:TestResults | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Header "Test Execution Complete"
Write-Host "  Total Duration: $($totalDuration.ToString('hh\:mm\:ss'))"
Write-Host "  Tests Run: $($script:TestResults.Count)"
Write-Host "  Passed: $passedCount ✅"
if ($failedCount -gt 0) {
    Write-Host "  Failed: $failedCount ❌" -ForegroundColor Red
}
else {
    Write-Host "  Failed: $failedCount"
}
Write-Host "  Report Location: $reportPath"

# Exit code
if ($failedCount -gt 0) {
    exit 1
}
else {
    exit 0
}

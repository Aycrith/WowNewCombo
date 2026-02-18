#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Phase 5: Feedback Loop Automation - Intelligent Test Analysis & Auto-Remediation
.DESCRIPTION
    Analyzes test failures, detects coverage regressions, and provides actionable feedback.
    Includes auto-retry for flaky tests and automated failure categorization.
.PARAMETER TestResultsPath
    Path to the test results TRX file
.PARAMETER CoverageReportPath
    Path to the coverage report
.PARAMETER BaselineCoveragePath
    Path to the baseline coverage report for regression detection
.PARAMETER OutputPath
    Path to write feedback reports
.PARAMETER MaxRetries
    Maximum retry attempts for flaky tests (default: 3)
.PARAMETER FlakyThreshold
    Failure count before marking test as flaky (default: 2)
.EXAMPLE
    .\feedback-loop-automation.ps1 -TestResultsPath "test-results.trx" -CoverageReportPath "coverage.cobertura.xml" -BaselineCoveragePath "baseline.cobertura.xml"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$TestResultsPath = "",
    
    [Parameter(Mandatory=$false)]
    [string]$CoverageReportPath = "",
    
    [Parameter(Mandatory=$false)]
    [string]$BaselineCoveragePath = "",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "docs/TestingFramework/Reports/Feedback",
    
    [int]$MaxRetries = 3,
    
    [int]$FlakyThreshold = 2
)

# Error handling
$ErrorActionPreference = "Stop"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

# Flaky test tracking database (simple JSON file)
$script:FlakyTestDbPath = Join-Path $OutputPath "flaky-tests-db.json"
$script:FlakyTestDb = @{}

function Load-FlakyTestDatabase {
    if (Test-Path $script:FlakyTestDbPath) {
        $script:FlakyTestDb = Get-Content $script:FlakyTestDbPath -Raw | ConvertFrom-Json -AsHashtable
        Write-ColorOutput "Loaded flaky test database: $($script:FlakyTestDb.Count) entries" "Gray"
    } else {
        $script:FlakyTestDb = @{}
    }
}

function Save-FlakyTestDatabase {
    $script:FlakyTestDb | ConvertTo-Json -Depth 10 | Set-Content $script:FlakyTestDbPath -Encoding UTF8
}

function Update-FlakyTestStatus {
    param([string]$TestName, [bool]$Passed)
    
    if (-not $script:FlakyTestDb.ContainsKey($TestName)) {
        $script:FlakyTestDb[$TestName] = @{
            TotalRuns = 0
            Failures = 0
            ConsecutiveFailures = 0
            LastRun = $null
            Status = "Unknown"
        }
    }
    
    $entry = $script:FlakyTestDb[$TestName]
    $entry.TotalRuns++
    $entry.LastRun = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    
    if ($Passed) {
        $entry.ConsecutiveFailures = 0
        if ($entry.Failures -gt 0) {
            $entry.Status = "Recovered"
        } else {
            $entry.Status = "Stable"
        }
    } else {
        $entry.Failures++
        $entry.ConsecutiveFailures++
        
        if ($entry.ConsecutiveFailures -ge $FlakyThreshold) {
            $entry.Status = "Flaky"
        } elseif ($entry.Failures -ge $FlakyThreshold) {
            $entry.Status = "PotentiallyFlaky"
        } else {
            $entry.Status = "Failing"
        }
    }
}

function Get-FlakyTests {
    return $script:FlakyTestDb.GetEnumerator() | 
        Where-Object { $_.Value.Status -in @("Flaky", "PotentiallyFlaky") } |
        Sort-Object { $_.Value.ConsecutiveFailures } -Descending
}

function Analyze-TestFailure {
    param([string]$TestName, [string]$ErrorMessage, [string]$StackTrace)
    
    $category = "Unknown"
    $suggestion = "Investigate manually"
    $priority = "Medium"
    
    # Pattern matching for common failure types
    if ($ErrorMessage -match "Assert.Equal\(\) Failure") {
        $category = "AssertionFailure"
        $suggestion = "Check expected vs actual values. Consider using more specific assertions."
        $priority = "High"
    }
    elseif ($ErrorMessage -match "null" -or $ErrorMessage -match "NullReference") {
        $category = "NullReference"
        $suggestion = "Add null checks or initialize objects properly in Arrange phase."
        $priority = "High"
    }
    elseif ($ErrorMessage -match "timeout" -or $ErrorMessage -match "timed out") {
        $category = "Timeout"
        $suggestion = "Consider increasing timeout or optimizing test performance."
        $priority = "Medium"
    }
    elseif ($ErrorMessage -match "mock" -or $ErrorMessage -match "Mock") {
        $category = "MockSetupError"
        $suggestion = "Verify mock setup and expectations. Check for missing Setup() calls."
        $priority = "High"
    }
    elseif ($ErrorMessage -match "file" -or $ErrorMessage -match "FileNotFound") {
        $category = "FileIO"
        $suggestion = "Check file paths and ensure test resources are available."
        $priority = "Medium"
    }
    elseif ($ErrorMessage -match "thread" -or $ErrorMessage -match "concurrent") {
        $category = "Concurrency"
        $suggestion = "Add synchronization or use thread-safe constructs."
        $priority = "High"
    }
    elseif ($StackTrace -match "async" -or $StackTrace -match "await") {
        $category = "AsyncDeadlock"
        $suggestion = "Check for .Result or .Wait() causing deadlocks. Use async/await throughout."
        $priority = "High"
    }
    elseif ($ErrorMessage -match "memory" -or $ErrorMessage -match "out of memory") {
        $category = "MemoryPressure"
        $suggestion = "Check for memory leaks or excessive allocations in test."
        $priority = "Critical"
    }
    else {
        # Check stack trace patterns
        if ($StackTrace -match "at.*Tests\.") {
            $category = "TestCodeError"
            $suggestion = "Review test implementation. Check Arrange/Act/Assert phases."
            $priority = "High"
        }
        elseif ($StackTrace -match "at Core\.") {
            $category = "ProductionCodeError"
            $suggestion = "Possible bug in production code. Review recent changes."
            $priority = "Critical"
        }
        else {
            $category = "ExternalDependency"
            $suggestion = "Check external dependencies (DB, API, file system)."
            $priority = "Medium"
        }
    }
    
    return [PSCustomObject]@{
        TestName = $TestName
        Category = $category
        Priority = $priority
        Suggestion = $suggestion
        ErrorMessage = $ErrorMessage
    }
}

function Parse-TestResults {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        Write-ColorOutput "Test results not found: $Path" "Yellow"
        return @()
    }
    
    Write-ColorOutput "Parsing test results..." "Yellow"
    $xml = [xml](Get-Content $Path)
    $results = @()
    
    foreach ($result in $xml.TestRun.Results.UnitTestResult) {
        $results += [PSCustomObject]@{
            TestName = $result.testName
            Outcome = $result.outcome
            Duration = $result.duration
            ErrorMessage = $result.Output.ErrorInfo.Message
            StackTrace = $result.Output.ErrorInfo.StackTrace
            ComputerName = $result.computerName
            ExecutionId = $result.executionId
        }
    }
    
    return $results
}

function Test-WithRetry {
    param(
        [string]$Filter,
        [int]$MaxAttempts = $MaxRetries
    )
    
    Write-ColorOutput "Running test with retry: $Filter (max $MaxAttempts attempts)" "Yellow"
    
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        Write-ColorOutput "  Attempt $attempt of $MaxAttempts..." "Gray"
        
        $result = dotnet test CoreUnitTests/CoreUnitTests.csproj `
            --filter "FullyQualifiedName~$Filter" `
            --no-build `
            --logger "console;verbosity=quiet" 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "  ✓ Passed on attempt $attempt" "Green"
            return @{ Success = $true; Attempts = $attempt }
        }
        
        if ($attempt -lt $MaxAttempts) {
            Write-ColorOutput "  ✗ Failed, waiting 2 seconds before retry..." "Yellow"
            Start-Sleep -Seconds 2
        }
    }
    
    Write-ColorOutput "  ✗ Failed after $MaxAttempts attempts" "Red"
    return @{ Success = $false; Attempts = $MaxAttempts }
}

function Detect-CoverageRegression {
    param([string]$CurrentPath, [string]$BaselinePath)
    
    if (-not (Test-Path $CurrentPath) -or -not (Test-Path $BaselinePath)) {
        Write-ColorOutput "Coverage reports not found for regression analysis" "Yellow"
        return @()
    }
    
    Write-ColorOutput "Detecting coverage regressions..." "Yellow"
    
    $current = [xml](Get-Content $CurrentPath)
    $baseline = [xml](Get-Content $BaselinePath)
    
    $regressions = @()
    
    # Build baseline lookup
    $baselineClasses = @{}
    foreach ($class in $baseline.coverage.packages.package.classes.class) {
        $key = "$($class.name):$($class.filename)"
        $baselineClasses[$key] = [decimal]$class.'line-rate'
    }
    
    # Check for regressions
    foreach ($class in $current.coverage.packages.package.classes.class) {
        $key = "$($class.name):$($class.filename)"
        $currentRate = [decimal]$class.'line-rate'
        $baselineRate = $baselineClasses[$key]
        
        if ($baselineRate -and $currentRate -lt $baselineRate - 0.05) { # 5% threshold
            $regressions += [PSCustomObject]@{
                Class = $class.name
                File = $class.filename
                Baseline = [math]::Round($baselineRate * 100, 2)
                Current = [math]::Round($currentRate * 100, 2)
                Drop = [math]::Round(($baselineRate - $currentRate) * 100, 2)
            }
        }
    }
    
    return $regressions | Sort-Object Drop -Descending
}

function Generate-FeedbackReport {
    param(
        [array]$TestResults,
        [array]$FailureAnalysis,
        [array]$Regressions,
        [array]$FlakyTests,
        [string]$ReportPath
    )
    
    $sb = New-Object System.Text.StringBuilder
    
    [void]$sb.AppendLine("# Phase 5: Feedback Loop Automation Report")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    [void]$sb.AppendLine("")
    
    # Summary
    [void]$sb.AppendLine("## Executive Summary")
    [void]$sb.AppendLine("")
    
    $passed = ($TestResults | Where-Object { $_.Outcome -eq "Passed" }).Count
    $failed = ($TestResults | Where-Object { $_.Outcome -eq "Failed" }).Count
    $skipped = ($TestResults | Where-Object { $_.Outcome -eq "Skipped" }).Count
    
    [void]$sb.AppendLine("| Metric | Value |")
    [void]$sb.AppendLine("|--------|-------|")
    [void]$sb.AppendLine("| Total Tests | $($TestResults.Count) |")
    [void]$sb.AppendLine("| Passed | $passed |")
    [void]$sb.AppendLine("| Failed | $failed |")
    [void]$sb.AppendLine("| Skipped | $skipped |")
    [void]$sb.AppendLine("| Coverage Regressions | $($Regressions.Count) |")
    [void]$sb.AppendLine("| Flaky Tests | $($FlakyTests.Count) |")
    [void]$sb.AppendLine("")
    
    # Failure Analysis
    if ($FailureAnalysis.Count -gt 0) {
        [void]$sb.AppendLine("## Failure Analysis")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("### Categorized Failures")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| Test | Category | Priority | Suggestion |")
        [void]$sb.AppendLine("|------|----------|----------|------------|")
        
        foreach ($failure in $FailureAnalysis) {
            $shortName = $failure.TestName -replace '^CoreUnitTests\.', ''
            [void]$sb.AppendLine("| $shortName | $($failure.Category) | $($failure.Priority) | $($failure.Suggestion) |")
        }
        [void]$sb.AppendLine("")
        
        # Priority breakdown
        [void]$sb.AppendLine("### By Priority")
        [void]$sb.AppendLine("")
        $byPriority = $FailureAnalysis | Group-Object Priority | Sort-Object { 
            switch ($_.Name) {
                "Critical" { 1 }
                "High" { 2 }
                "Medium" { 3 }
                "Low" { 4 }
                default { 5 }
            }
        }
        
        foreach ($group in $byPriority) {
            [void]$sb.AppendLine("- **$($group.Name)**: $($group.Count) tests")
        }
        [void]$sb.AppendLine("")
    }
    
    # Coverage Regressions
    if ($Regressions.Count -gt 0) {
        [void]$sb.AppendLine("## Coverage Regressions")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| Class | Baseline | Current | Drop |")
        [void]$sb.AppendLine("|-------|----------|---------|------|")
        
        foreach ($reg in $Regressions) {
            [void]$sb.AppendLine("| $($reg.Class) | $($reg.Baseline)% | $($reg.Current)% | -$($reg.Drop)% |")
        }
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("⚠️ **Action Required**: Coverage has decreased significantly in these classes.")
        [void]$sb.AppendLine("")
    }
    
    # Flaky Tests
    if ($FlakyTests.Count -gt 0) {
        [void]$sb.AppendLine("## Flaky Tests Detected")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| Test | Status | Failures | Consecutive |")
        [void]$sb.AppendLine("|------|--------|----------|-------------|")
        
        foreach ($flaky in $FlakyTests) {
            [void]$sb.AppendLine("| $($flaky.Key) | $($flaky.Value.Status) | $($flaky.Value.Failures) | $($flaky.Value.ConsecutiveFailures) |")
        }
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("These tests are being auto-retried in CI/CD pipeline.")
        [void]$sb.AppendLine("")
    }
    
    # Recommendations
    [void]$sb.AppendLine("## Recommendations")
    [void]$sb.AppendLine("")
    
    if ($failed -gt 0) {
        [void]$sb.AppendLine("1. **Fix Critical Failures First** - $(($FailureAnalysis | Where-Object { $_.Priority -eq 'Critical' }).Count) critical failures need immediate attention")
    }
    
    if ($Regressions.Count -gt 0) {
        [void]$sb.AppendLine("2. **Address Coverage Regressions** - $($Regressions.Count) classes have significant coverage drops")
    }
    
    if ($FlakyTests.Count -gt 0) {
        [void]$sb.AppendLine("3. **Stabilize Flaky Tests** - $($FlakyTests.Count) tests are marked as flaky")
    }
    
    [void]$sb.AppendLine("4. **Review Failure Categories** - Focus on $(($FailureAnalysis | Group-Object Category | Sort-Object Count -Descending | Select-Object -First 1).Name) issues")
    [void]$sb.AppendLine("")
    
    # Action Items
    [void]$sb.AppendLine("## Action Items")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- [ ] Review all Critical priority failures")
    [void]$sb.AppendLine("- [ ] Investigate coverage regressions")
    [void]$sb.AppendLine("- [ ] Stabilize or quarantine flaky tests")
    [void]$sb.AppendLine("- [ ] Update documentation for common failure patterns")
    [void]$sb.AppendLine("")
    
    [void]$sb.AppendLine("---")
    [void]$sb.AppendLine("*Generated by Phase 5: Feedback Loop Automation*")
    
    Set-Content -Path $ReportPath -Value $sb.ToString() -Encoding UTF8
}

# Main execution
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput " Phase 5: Feedback Loop Automation" "Cyan"
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput ""

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Load flaky test database
Load-FlakyTestDatabase

# Parse test results
$testResults = @()
$failureAnalysis = @()

if (Test-Path $TestResultsPath) {
    $testResults = Parse-TestResults -Path $TestResultsPath
    
    Write-ColorOutput "Found $($testResults.Count) test results" "Yellow"
    
    # Analyze failures
    $failures = $testResults | Where-Object { $_.Outcome -eq "Failed" }
    Write-ColorOutput "Analyzing $($failures.Count) failures..." "Yellow"
    
    foreach ($failure in $failures) {
        $analysis = Analyze-TestFailure `
            -TestName $failure.TestName `
            -ErrorMessage $failure.ErrorMessage `
            -StackTrace $failure.StackTrace
        
        $failureAnalysis += $analysis
        Update-FlakyTestStatus -TestName $failure.TestName -Passed $false
    }
    
    # Update passed tests
    $passed = $testResults | Where-Object { $_.Outcome -eq "Passed" }
    foreach ($test in $passed) {
        Update-FlakyTestStatus -TestName $test.TestName -Passed $true
    }
    
    Save-FlakyTestDatabase
} else {
    Write-ColorOutput "No test results file provided, running tests now..." "Yellow"
    
    # Run tests and capture results
    $tempResults = Join-Path $OutputPath "temp-results.trx"
    dotnet test CoreUnitTests/CoreUnitTests.csproj `
        --logger "trx;LogFileName=$tempResults" `
        --verbosity quiet 2>&1
    
    if (Test-Path $tempResults) {
        $testResults = Parse-TestResults -Path $tempResults
        Remove-Item $tempResults
    }
}

# Get flaky tests
$flakyTests = Get-FlakyTests
if ($flakyTests.Count -gt 0) {
    Write-ColorOutput ""
    Write-ColorOutput "Detected $($flakyTests.Count) flaky tests:" "Yellow"
    foreach ($flaky in $flakyTests | Select-Object -First 5) {
        Write-ColorOutput "  - $($flaky.Key): $($flaky.Value.ConsecutiveFailures) consecutive failures" "Red"
    }
}

# Detect coverage regressions
$regressions = @()
if ($CoverageReportPath -and $BaselineCoveragePath) {
    $regressions = Detect-CoverageRegression `
        -CurrentPath $CoverageReportPath `
        -BaselinePath $BaselineCoveragePath
    
    if ($regressions.Count -gt 0) {
        Write-ColorOutput ""
        Write-ColorOutput "⚠️ Detected $($regressions.Count) coverage regressions!" "Red"
        foreach ($reg in $regressions | Select-Object -First 5) {
            Write-ColorOutput "  - $($reg.Class): $($reg.Baseline)% → $($reg.Current)% (-$($reg.Drop)%)" "Red"
        }
    } else {
        Write-ColorOutput ""
        Write-ColorOutput "✓ No coverage regressions detected" "Green"
    }
}

# Generate report
$reportPath = Join-Path $OutputPath "feedback-report.md"
Generate-FeedbackReport `
    -TestResults $testResults `
    -FailureAnalysis $failureAnalysis `
    -Regressions $regressions `
    -FlakyTests $flakyTests `
    -ReportPath $reportPath

Write-ColorOutput ""
Write-ColorOutput "Feedback report saved to: $reportPath" "Green"

# Summary
Write-ColorOutput ""
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput " Feedback Loop Summary" "Cyan"
Write-ColorOutput "==========================================" "Cyan"

$passed = ($testResults | Where-Object { $_.Outcome -eq "Passed" }).Count
$failed = ($testResults | Where-Object { $_.Outcome -eq "Failed" }).Count

Write-ColorOutput "Tests analyzed: $($testResults.Count)" "White"
Write-ColorOutput "  Passed: $passed" "Green"
Write-ColorOutput "  Failed: $failed" $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-ColorOutput "  Analyzed: $($failureAnalysis.Count)" "Yellow"
Write-ColorOutput "Coverage regressions: $($regressions.Count)" $(if ($regressions.Count -gt 0) { "Red" } else { "Green" })
Write-ColorOutput "Flaky tests tracked: $($flakyTests.Count)" "Yellow"

Write-ColorOutput ""
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput " Phase 5 Complete!" "Cyan"
Write-ColorOutput "==========================================" "Cyan"

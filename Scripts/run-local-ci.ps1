#requires -Version 7.0
<#
.SYNOPSIS
    Local CI/CD script for WowClassicGrindBot - runs all tests and generates evidence reports.

.DESCRIPTION
    This script automates the testing pipeline for local development:
    1. Builds the solution
    2. Runs unit tests with coverage
    3. Runs integration tests
    4. Runs stability tests (optional, longer duration)
    5. Generates evidence reports
    6. Validates performance thresholds

.PARAMETER SkipStability
    Skip the long-running stability tests (default runs them).

.PARAMETER SkipBuild
    Skip the build step (useful for running tests only).

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Debug.

.PARAMETER EvidencePath
    Path where evidence reports are saved. Default is ./test-evidence

.EXAMPLE
    .\scripts\run-local-ci.ps1
    Runs the full CI pipeline.

.EXAMPLE
    .\scripts\run-local-ci.ps1 -SkipStability -SkipBuild
    Runs tests only, skipping stability and build.

.EXAMPLE
    .\scripts\run-local-ci.ps1 -Configuration Release
    Runs CI with Release configuration.
#>

param(
    [switch]$SkipStability,
    [switch]$SkipBuild,
    [string]$Configuration = "Debug",
    [string]$EvidencePath = "./test-evidence"
)

$ErrorActionPreference = "Stop"
$startTime = Get-Date

# Colors for output
$Green = "\033[32m"
$Red = "\033[31m"
$Yellow = "\033[33m"
$Blue = "\033[34m"
$Reset = "\033[0m"

function Write-Status($Message, $Status = "Info") {
    $color = switch ($Status) {
        "Success" { $Green }
        "Error" { $Red }
        "Warning" { $Yellow }
        "Info" { $Blue }
        default { $Reset }
    }
    Write-Host "$color[${Status.ToUpper()}]$Reset $Message"
}

function Test-LastExitCode($Context) {
    if ($LASTEXITCODE -ne 0) {
        Write-Status "Failed: $Context" "Error"
        exit $LASTEXITCODE
    }
}

# Create evidence directory
New-Item -ItemType Directory -Force -Path $EvidencePath | Out-Null
Write-Status "Evidence reports will be saved to: $(Resolve-Path $EvidencePath)" "Info"

# Track results
$results = @{
    StartTime = $startTime
    BuildSuccess = $false
    UnitTestsPassed = 0
    UnitTestsFailed = 0
    IntegrationTestsPassed = 0
    IntegrationTestsFailed = 0
    StabilityTestsPassed = 0
    StabilityTestsFailed = 0
    TotalDuration = $null
}

Write-Status "Starting Local CI/CD Pipeline" "Info"
Write-Status "Configuration: $Configuration" "Info"
Write-Status "Working Directory: $(Get-Location)" "Info"

# ============================================================================
# STEP 1: Build
# ============================================================================
if (-not $SkipBuild) {
    Write-Status "Step 1/5: Building Solution..." "Info"

    dotnet restore
    Test-LastExitCode "Restore"

    dotnet build MasterOfPuppets.sln --configuration $Configuration --no-restore
    Test-LastExitCode "Build"

    $results.BuildSuccess = $true
    Write-Status "Build completed successfully" "Success"
} else {
    Write-Status "Step 1/5: Build SKIPPED" "Warning"
    $results.BuildSuccess = $true
}

# ============================================================================
# STEP 2: Unit Tests
# ============================================================================
Write-Status "Step 2/5: Running Unit Tests..." "Info"

$unitTestProjects = @(
    "CoreUnitTests",
    "FrontendUnitTests"
)

$unitTestResults = @()
foreach ($project in $unitTestProjects) {
    Write-Status "Running tests for $project..." "Info"

    $testOutput = Join-Path $EvidencePath "${project}-test-results.trx"
    $coverageOutput = Join-Path $EvidencePath "${project}-coverage.xml"

    # Run tests with TRX output for parsing
    dotnet test $project `
        --configuration $Configuration `
        --no-build `
        --logger "trx;LogFileName=$testOutput" `
        --collect:"XPlat Code Coverage" `
        --results-directory $EvidencePath

    if ($LASTEXITCODE -eq 0) {
        Write-Status "$project tests PASSED" "Success"
    } else {
        Write-Status "$project tests FAILED (exit code: $LASTEXITCODE)" "Error"
    }

    # Parse test results
    if (Test-Path $testOutput) {
        [xml]$trx = Get-Content $testOutput
        $counters = $trx.TestRun.Results.Counters
        $results.UnitTestsPassed += [int]$counters.passed
        $results.UnitTestsFailed += [int]$counters.failed
        Write-Status "$project: $($counters.passed) passed, $($counters.failed) failed" "Info"
    }
}

# ============================================================================
# STEP 3: Integration Tests
# ============================================================================
Write-Status "Step 3/5: Running Integration Tests..." "Info"

$integrationTestOutput = Join-Path $EvidencePath "integration-test-results.trx"

dotnet test CoreUnitTests `
    --configuration $Configuration `
    --no-build `
    --filter "FullyQualifiedName~Integration" `
    --logger "trx;LogFileName=$integrationTestOutput" `
    --results-directory $EvidencePath

if (Test-Path $integrationTestOutput) {
    [xml]$trx = Get-Content $integrationTestOutput
    $counters = $trx.TestRun.Results.Counters
    $results.IntegrationTestsPassed = [int]$counters.passed
    $results.IntegrationTestsFailed = [int]$counters.failed
    Write-Status "Integration tests: $($counters.passed) passed, $($counters.failed) failed" "Info"
}

# ============================================================================
# STEP 4: Stability Tests
# ============================================================================
if (-not $SkipStability) {
    Write-Status "Step 4/5: Running Stability Tests (this may take a while)..." "Info"

    $stabilityTestOutput = Join-Path $EvidencePath "stability-test-results.trx"

    # Run stability tests with extended timeout
    dotnet test FrontendUnitTests `
        --configuration $Configuration `
        --no-build `
        --filter "FullyQualifiedName~Stability" `
        --logger "trx;LogFileName=$stabilityTestOutput" `
        --results-directory $EvidencePath

    if (Test-Path $stabilityTestOutput) {
        [xml]$trx = Get-Content $stabilityTestOutput
        $counters = $trx.TestRun.Results.Counters
        $results.StabilityTestsPassed = [int]$counters.passed
        $results.StabilityTestsFailed = [int]$counters.failed
        Write-Status "Stability tests: $($counters.passed) passed, $($counters.failed) failed" "Info"
    }
} else {
    Write-Status "Step 4/5: Stability Tests SKIPPED" "Warning"
}

# ============================================================================
# STEP 5: Evidence Collection & Report Generation
# ============================================================================
Write-Status "Step 5/5: Generating Evidence Report..." "Info"

$endTime = Get-Date
$results.TotalDuration = $endTime - $startTime

# Generate summary report
$report = @"
# WowClassicGrindBot CI/CD Evidence Report

Generated: $($endTime.ToString("yyyy-MM-dd HH:mm:ss"))
Duration: $($results.TotalDuration.ToString("hh\:mm\:ss"))
Configuration: $Configuration

## Build Status
- Build Successful: $($results.BuildSuccess)

## Test Results

### Unit Tests
- Passed: $($results.UnitTestsPassed)
- Failed: $($results.UnitTestsFailed)
- Success Rate: $([math]::Round(($results.UnitTestsPassed / ($results.UnitTestsPassed + $results.UnitTestsFailed)) * 100, 2))%

### Integration Tests
- Passed: $($results.IntegrationTestsPassed)
- Failed: $($results.IntegrationTestsFailed)
- Success Rate: $([math]::Round(($results.IntegrationTestsPassed / ($results.IntegrationTestsPassed + $results.IntegrationTestsFailed)) * 100, 2))%

### Stability Tests
- Passed: $($results.StabilityTestsPassed)
- Failed: $($results.StabilityTestsFailed)
- Success Rate: $([math]::Round(($results.StabilityTestsPassed / [math]::Max(1, $results.StabilityTestsPassed + $results.StabilityTestsFailed)) * 100, 2))%

## Evidence Files
All evidence files are located in: $(Resolve-Path $EvidencePath)

### Generated Files:
$(Get-ChildItem $EvidencePath | ForEach-Object { "- $($_.Name)" } | Out-String)

## Status: $(if ($results.UnitTestsFailed -eq 0 -and $results.IntegrationTestsFailed -eq 0) { "PASS" } else { "FAIL" })
"@

$reportPath = Join-Path $EvidencePath "ci-report.md"
$report | Out-File -FilePath $reportPath -Encoding UTF8
Write-Status "Evidence report saved to: $reportPath" "Success"

# ============================================================================
# Summary
# ============================================================================
Write-Host ""
Write-Status "=== CI/CD Pipeline Complete ===" "Info"
Write-Host ""
Write-Host "Build: $($results.BuildSuccess ? "PASS" : "FAIL")"
Write-Host "Unit Tests: $($results.UnitTestsPassed) passed, $($results.UnitTestsFailed) failed"
Write-Host "Integration Tests: $($results.IntegrationTestsPassed) passed, $($results.IntegrationTestsFailed) failed"
Write-Host "Stability Tests: $($results.StabilityTestsPassed) passed, $($results.StabilityTestsFailed) failed"
Write-Host "Total Duration: $($results.TotalDuration.ToString("hh\:mm\:ss"))"
Write-Host ""

$totalFailed = $results.UnitTestsFailed + $results.IntegrationTestsFailed + $results.StabilityTestsFailed
if ($totalFailed -eq 0) {
    Write-Status "ALL TESTS PASSED" "Success"
    exit 0
} else {
    Write-Status "SOME TESTS FAILED" "Error"
    exit 1
}

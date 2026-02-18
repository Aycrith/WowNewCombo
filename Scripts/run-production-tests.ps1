#Requires -Version 7.0
<#
.SYNOPSIS
    Production-ready unified test runner for WowClassicGrindBot.

.DESCRIPTION
    Runs all test projects with proper categorization, evidence collection,
    and reporting. Designed for CI/CD and local development.

.PARAMETER Category
    Test categories to run: Unit, Integration, Evidence, Stability, Smoke, All

.PARAMETER Configuration
    Build configuration: Debug or Release

.PARAMETER EvidencePath
    Path for evidence reports

.PARAMETER FailOnWarnings
    Treat warnings as failures

.PARAMETER Parallel
    Run tests in parallel

.PARAMETER SkipBuild
    Skip the build step

.EXAMPLE
    .\Scripts\run-production-tests.ps1 -Category All

    Runs all tests with default settings.

.EXAMPLE
    .\Scripts\run-production-tests.ps1 -Category Integration -Configuration Release

    Runs integration tests in Release mode.

.EXAMPLE
    .\Scripts\run-production-tests.ps1 -Category Smoke

    Runs smoke tests only (fast validation).
#>
[CmdletBinding()]
param(
    [ValidateSet("Unit", "Integration", "Evidence", "Stability", "Smoke", "All")]
    [string]$Category = "All",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$EvidencePath = "./test-evidence",

    [switch]$FailOnWarnings,

    [switch]$Parallel,

    [switch]$SkipBuild
)

# Error handling
$ErrorActionPreference = "Stop"
$ProgressPreference = "Continue"

# Colors for output
$colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
}

function Write-Status($Message, $Level = "Info") {
    Write-Host "[$Level] $Message" -ForegroundColor $colors[$Level]
}

function Test-LastExitCode($Context) {
    if ($LASTEXITCODE -ne 0) {
        Write-Status "Failed: $Context" "Error"
        throw "Test execution failed for: $Context"
    }
}

# Initialize
$startTime = Get-Date
$results = @{
    StartTime = $startTime
    BuildSuccess = $false
    Unit = @{ Passed = 0; Failed = 0; Duration = $null; Skipped = 0 }
    Integration = @{ Passed = 0; Failed = 0; Duration = $null; Skipped = 0 }
    Evidence = @{ Passed = 0; Failed = 0; Duration = $null; Skipped = 0 }
    Stability = @{ Passed = 0; Failed = 0; Duration = $null; Skipped = 0 }
    Smoke = @{ Passed = 0; Failed = 0; Duration = $null; Skipped = 0 }
}

New-Item -ItemType Directory -Force -Path $EvidencePath | Out-Null

Write-Status "Production Test Runner Started" "Info"
Write-Status "Category: $Category | Configuration: $Configuration" "Info"
Write-Status "Evidence Path: $(Resolve-Path $EvidencePath)" "Info"

# Step 1: Build
if (-not $SkipBuild) {
    Write-Status "Building solution..." "Info"
    dotnet restore | Out-Null
    Test-LastExitCode "Restore"

    $buildArgs = @(
        "build", "MasterOfPuppets.sln",
        "--configuration", $Configuration,
        "--no-restore"
    )

    if ($FailOnWarnings) {
        $buildArgs += "-p:TreatWarningsAsErrors=true"
    }

    dotnet @buildArgs
    Test-LastExitCode "Build"

    $results.BuildSuccess = $true
    Write-Status "Build successful" "Success"
} else {
    $results.BuildSuccess = $true
    Write-Status "Build skipped" "Warning"
}

# Step 2: Run Tests by Category
function Invoke-TestCategory {
    param($Name, $Project, $Filter, $IsSmoke = $false)

    Write-Status "Running $Name tests..." "Info"
    $categoryStart = Get-Date

    $testArgs = @(
        $Project
        "--configuration", $Configuration
        "--no-build"
        "--logger", "trx;LogFileName=$EvidencePath/${Name}Results.trx"
        "--results-directory", $EvidencePath
    )

    if ($Filter) {
        $testArgs += "--filter", $Filter
    }

    if ($Parallel) {
        $testArgs += "--parallel"
    }

    if ($FailOnWarnings) {
        $testArgs += "--logger", "console;verbosity=detailed"
    }

    $output = & dotnet test @testArgs 2>&1
    $exitCode = $LASTEXITCODE

    $duration = (Get-Date) - $categoryStart
    $results[$Name].Duration = $duration

    # Parse results from TRX file
    $trxFile = "$EvidencePath/${Name}Results.trx"
    if (Test-Path $trxFile) {
        [xml]$trx = Get-Content $trxFile
        $counters = $trx.TestRun.ResultSummary.Counters
        if ($counters) {
            $results[$Name].Passed = [int]$counters.passed
            $results[$Name].Failed = [int]$counters.failed
            $results[$Name].Skipped = [int]$counters.skipped
        }
    }

    if ($exitCode -eq 0) {
        Write-Status "$Name tests PASSED ($($results[$Name].Passed) passed, $($results[$Name].Failed) failed, $($results[$Name].Skipped) skipped)" "Success"
    } else {
        Write-Status "$Name tests FAILED ($($results[$Name].Failed) failed)" "Error"
    }

    return $exitCode
}

# Smoke tests - quick validation of critical paths
if ($Category -eq "Smoke") {
    Write-Status "Running Smoke Tests..." "Info"

    # Run critical path tests only
    $smokeFilter = "FullyQualifiedName~CircuitBreaker|FullyQualifiedName~FeatureFlag|FullyQualifiedName~IntegrationTestBase"
    $exitCode = Invoke-TestCategory -Name "Smoke" -Project "CoreUnitTests" -Filter $smokeFilter -IsSmoke $true

    if ($exitCode -ne 0) {
        Write-Status "SMOKE TESTS FAILED" "Error"
        exit $exitCode
    }

    Write-Status "Smoke tests passed in $($results.Smoke.Duration.ToString('mm\:ss'))" "Success"
}

# Run categories based on selection
if ($Category -in @("Unit", "All")) {
    $exitCode = Invoke-TestCategory -Name "Unit" -Project "CoreUnitTests" -Filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Evidence&FullyQualifiedName!~Stability&FullyQualifiedName!~Scenario"
    if ($exitCode -ne 0 -and $FailOnWarnings) { exit $exitCode }
}

if ($Category -in @("Unit", "All")) {
    $exitCode = Invoke-TestCategory -Name "Unit" -Project "FrontendUnitTests" -Filter "FullyQualifiedName!~Stability"
    if ($exitCode -ne 0 -and $FailOnWarnings) { exit $exitCode }
}

if ($Category -in @("Integration", "All")) {
    $exitCode = Invoke-TestCategory -Name "Integration" -Project "CoreUnitTests" -Filter "FullyQualifiedName~Integration"
    if ($exitCode -ne 0 -and $FailOnWarnings) { exit $exitCode }
}

if ($Category -in @("Evidence", "All")) {
    $exitCode = Invoke-TestCategory -Name "Evidence" -Project "CoreUnitTests" -Filter "FullyQualifiedName~Evidence"
    if ($exitCode -ne 0 -and $FailOnWarnings) { exit $exitCode }
}

if ($Category -in @("Stability", "All")) {
    $exitCode = Invoke-TestCategory -Name "Stability" -Project "FrontendUnitTests" -Filter "FullyQualifiedName~Stability"
    if ($exitCode -ne 0 -and $FailOnWarnings) { exit $exitCode }
}

# Step 3: Generate Report
$endTime = Get-Date
$totalDuration = $endTime - $startTime

$report = @"
# WowClassicGrindBot Production Test Report

**Generated:** $($endTime.ToString("yyyy-MM-dd HH:mm:ss"))
**Total Duration:** $($totalDuration.ToString("hh\:mm\:ss"))
**Configuration:** $Configuration

## Summary

| Category | Passed | Failed | Skipped | Duration |
|----------|--------|--------|---------|----------|
$(
    @("Unit", "Integration", "Evidence", "Stability", "Smoke") | ForEach-Object {
        $cat = $_
        $r = $results[$cat]
        if ($r.Duration) {
            "| $cat | $($r.Passed) | $($r.Failed) | $($r.Skipped) | $($r.Duration.ToString("mm\:ss")) |"
        }
    }
)

## Status: $(if (($results.Unit.Failed + $results.Integration.Failed + $results.Evidence.Failed + $results.Stability.Failed + $results.Smoke.Failed) -eq 0) { "PASS" } else { "FAIL" })

## Evidence Files

All test results and evidence saved to: $(Resolve-Path $EvidencePath)

## Test Projects

- **CoreUnitTests**: ~1000+ unit and integration tests
- **FrontendUnitTests**: 29 Blazor component tests
- **CoreManualTests**: Manual integration runner (not automated)

---
*Generated by run-production-tests.ps1*
"@

$reportPath = "$EvidencePath/production-test-report.md"
$report | Out-File -FilePath $reportPath -Encoding UTF8

Write-Status "Report saved to: $reportPath" "Info"
Write-Status "=== Test Run Complete ===" "Info"

# Exit with appropriate code
$totalFailed = $results.Unit.Failed + $results.Integration.Failed + $results.Evidence.Failed + $results.Stability.Failed + $results.Smoke.Failed
if ($totalFailed -eq 0) {
    Write-Status "ALL TESTS PASSED" "Success"
    exit 0
} else {
    Write-Status "$totalFailed TEST(S) FAILED" "Error"
    exit 1
}

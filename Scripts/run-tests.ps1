#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated test runner for WowClassicGrindBot CI/CD pipeline.

.DESCRIPTION
    This script runs all tests and generates reports for CI/CD integration.
    It supports running specific test categories and failing the build if tests don't pass.

.PARAMETER TestCategory
    Filter tests by category. Options: All, E2E, MockWoWClient, PixelEncoding, InterfaceCompliance

.PARAMETER Configuration
    Build configuration. Options: Debug, Release

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\run-tests.ps1 -TestCategory All
    Runs all tests

.EXAMPLE
    .\run-tests.ps1 -TestCategory E2E -Configuration Release
    Runs E2E tests in Release mode
#>

param(
    [ValidateSet("All", "E2E", "MockWoWClient", "PixelEncoding", "InterfaceCompliance")]
    [string]$TestCategory = "All",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"
$ExitCode = 0

function Write-Header($Message) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host $Message -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
}

function Write-Success($Message) {
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Error($Message) {
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Warning($Message) {
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Info($Message) {
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

# Get script directory and solution root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir
$SolutionFile = Join-Path $SolutionRoot "MasterOfPuppets.sln"

Write-Header "WowClassicGrindBot Test Harness"
Write-Info "Solution: $SolutionFile"
Write-Info "Configuration: $Configuration"
Write-Info "Test Category: $TestCategory"

# Build the solution first
Write-Header "Building Solution"
$BuildArgs = @("build", $SolutionFile, "-c", $Configuration)
if ($VerboseOutput) { $BuildArgs += @("-v", "n") }

Write-Info "Running: dotnet $BuildArgs"
& dotnet @BuildArgs 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Success "Build completed successfully"

# Run tests
Write-Header "Running Tests"

$TestProject = Join-Path $SolutionRoot "CoreUnitTests\CoreUnitTests.csproj"

if (-not (Test-Path $TestProject)) {
    Write-Error "Test project not found: $TestProject"
    exit 1
}

$TestArgs = @("test", $TestProject, "-c", $Configuration, "--no-build")

# Add test filter based on category
if ($TestCategory -eq "E2E") {
    $TestArgs += @("--filter", "FullyQualifiedName~EndToEnd")
} elseif ($TestCategory -eq "MockWoWClient") {
    $TestArgs += @("--filter", "FullyQualifiedName~MockWoWClient")
} elseif ($TestCategory -eq "PixelEncoding") {
    $TestArgs += @("--filter", "FullyQualifiedName~PixelEncoding")
} elseif ($TestCategory -eq "InterfaceCompliance") {
    $TestArgs += @("--filter", "FullyQualifiedName~InterfaceCompliance")
}

if ($VerboseOutput) {
    $TestArgs += @("-v", "n")
} else {
    $TestArgs += @("-v", "q")
}

Write-Info "Running: dotnet $TestArgs"
$TestOutput = & dotnet @TestArgs 2>&1
$TestOutput | Write-Host

# Check exit code
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed with exit code $LASTEXITCODE"
    $ExitCode = 1
}

# Parse test results from output
$TotalMatch = $TestOutput | Select-String "Total tests:\s+(\d+)"
$PassedMatch = $TestOutput | Select-String "Passed:\s+(\d+)"
$FailedMatch = $TestOutput | Select-String "Failed:\s+(\d+)"

if ($TotalMatch) {
    $Total = $TotalMatch.Matches[0].Groups[1].Value
    $Passed = if ($PassedMatch) { $PassedMatch.Matches[0].Groups[1].Value } else { "0" }
    $Failed = if ($FailedMatch) { $FailedMatch.Matches[0].Groups[1].Value } else { "0" }
    
    Write-Host ""
    Write-Host "Test Results:"
    Write-Host "  Total: $Total" -ForegroundColor White
    Write-Host "  Passed: $Passed" -ForegroundColor Green
    Write-Host "  Failed: $Failed" -ForegroundColor $(if ([int]$Failed -gt 0) { "Red" } else { "Green" })
    
    if ([int]$Failed -eq 0) {
        Write-Success "All tests passed!"
    } else {
        Write-Error "Some tests failed!"
        $ExitCode = 1
    }
}

# Output final status
Write-Header "Final Status"
if ($ExitCode -eq 0) {
    Write-Success "Build and test execution completed successfully!"
} else {
    Write-Error "Build or test execution failed!"
}

exit $ExitCode

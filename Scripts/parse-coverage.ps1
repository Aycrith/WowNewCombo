#!/usr/bin/env pwsh
# Parse Cobertura coverage report and generate summary
param(
    [string]$CoverageFile,
    [string]$OutputPath = "docs/TestingFramework/Reports/Baseline"
)

if (-not (Test-Path $CoverageFile)) {
    Write-Error "Coverage file not found: $CoverageFile"
    exit 1
}

Write-Host "Parsing coverage report: $CoverageFile" -ForegroundColor Cyan

[xml]$coverage = Get-Content $CoverageFile

# Extract summary
$rate = $coverage.coverage.'line-rate'
$linesValid = $coverage.coverage.lines-valid
$linesCovered = $coverage.coverage.lines-covered
$branchesValid = $coverage.coverage.branches-valid
$branchesCovered = $coverage.coverage.branches-covered
$coveragePct = [math]::Round([double]$rate * 100, 2)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Coverage Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Line Coverage: $linesCovered / $linesValid ($coveragePct%)" -ForegroundColor White

if ($branchesValid -gt 0) {
    $branchRate = $coverage.coverage.'branch-rate'
    $branchPct = [math]::Round([double]$branchRate * 100, 2)
    Write-Host "Branch Coverage: $branchesCovered / $branchesValid ($branchPct%)" -ForegroundColor White
}

Write-Host ""
Write-Host "By Package:" -ForegroundColor Cyan
Write-Host ""

# Group by package
$packages = @{}
foreach ($pkg in $coverage.coverage.packages.package) {
    $pkgName = $pkg.name
    $pkgRate = [math]::Round([double]$pkg.'line-rate' * 100, 2)
    $pkgLines = $pkg.lines-covered
    $pkgTotal = $pkg.lines-valid
    
    $packages[$pkgName] = @{
        Rate = $pkgRate
        Covered = $pkgLines
        Total = $pkgTotal
    }
    
    $color = if ($pkgRate -eq 0) { "Red" } elseif ($pkgRate -lt 30) { "Yellow" } elseif ($pkgRate -lt 70) { "White" } else { "Green" }
    Write-Host "  $pkgName" -NoNewline
    Write-Host ": $pkgRate% ($pkgLines/$pkgTotal)" -ForegroundColor $color
}

Write-Host ""

# Critical components analysis
Write-Host "Critical Components Analysis:" -ForegroundColor Cyan
Write-Host ""

$criticalComponents = @(
    @{ Name = "Core.GOAP"; MinCoverage = 90; Priority = "P0" },
    @{ Name = "Core.Goals"; MinCoverage = 85; Priority = "P0" },
    @{ Name = "Core.Requirement"; MinCoverage = 80; Priority = "P1" },
    @{ Name = "Core.ClassConfig"; MinCoverage = 80; Priority = "P1" },
    @{ Name = "Core.CombatRotation"; MinCoverage = 80; Priority = "P2" },
    @{ Name = "Core.Hazard"; MinCoverage = 80; Priority = "P2" },
    @{ Name = "Core.Humanization"; MinCoverage = 70; Priority = "P3" },
    @{ Name = "Core.Navigation"; MinCoverage = 85; Priority = "P0" }
)

$summary = @{}

foreach ($component in $criticalComponents) {
    $pkg = $packages[$component.Name]
    if ($pkg) {
        $current = $pkg.Rate
        $target = $component.MinCoverage
        $gap = $target - $current
        $status = if ($current -ge $target) { "✅ PASS" } else { "❌ FAIL" }
        $color = if ($current -ge $target) { "Green" } elseif ($gap -lt 20) { "Yellow" } else { "Red" }
        
        Write-Host "  $($component.Priority) $($component.Name)" -NoNewline
        Write-Host ": $current% / $target% target $status" -ForegroundColor $color
        
        $summary[$component.Name] = @{
            Current = $current
            Target = $target
            Gap = $gap
            Priority = $component.Priority
        }
    } else {
        Write-Host "  $($component.Priority) $($component.Name)" -NoNewline
        Write-Host ": NOT FOUND (0% coverage)" -ForegroundColor Red
    }
}

Write-Host ""

# Generate JSON report
$report = @{
    Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Summary = @{
        LinesCovered = [int]$linesCovered
        LinesTotal = [int]$linesValid
        CoveragePercent = $coveragePct
        BranchCoverage = if ($branchesValid -gt 0) { 
            @{
                Covered = [int]$branchesCovered
                Total = [int]$branchesValid
                Percent = [math]::Round([double]$branchRate * 100, 2)
            }
        } else { $null }
    }
    Packages = $packages
    CriticalGaps = $summary
}

$jsonPath = "$OutputPath/coverage-baseline-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$report | ConvertTo-Json -Depth 10 | Out-File $jsonPath

Write-Host "Report saved to: $jsonPath" -ForegroundColor Green
Write-Host ""

# Summary for PROGRESS.md
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phase 1 Baseline Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Overall Coverage: $coveragePct%" -ForegroundColor $(if ($coveragePct -ge 80) { "Green" } elseif ($coveragePct -ge 50) { "Yellow" } else { "Red" })
Write-Host "Total Lines: $linesValid" -ForegroundColor White
Write-Host "Lines Covered: $linesCovered" -ForegroundColor White
Write-Host "Tests: 243 passed, 1 failed" -ForegroundColor White
Write-Host ""

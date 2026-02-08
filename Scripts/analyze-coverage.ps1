#!/usr/bin/env pwsh
# Comprehensive Testing Framework - Coverage Analysis Script
# Phase 1: Baseline Assessment

param(
    [string]$OutputPath = "docs/TestingFramework/Reports/Baseline",
    [string]$Configuration = "Debug",
    [switch]$SkipHtmlReport,
    [switch]$SkipJsonReport
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Baseline Coverage Analysis" -ForegroundColor Cyan
Write-Host "  WowClassicGrindBot Testing Framework" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "Created output directory: $OutputPath" -ForegroundColor Green
}

$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$coverageDir = "$OutputPath/coverage_$timestamp"
New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null

Write-Host "Running coverage analysis..." -ForegroundColor Yellow
Write-Host "Output: $coverageDir" -ForegroundColor Gray
Write-Host ""

# Run tests with coverage collection
try {
    Write-Host "Step 1/4: Running tests with coverage collection..." -ForegroundColor Cyan
    
    dotnet test CoreUnitTests/CoreUnitTests.csproj `
        --configuration $Configuration `
        --no-build `
        --collect:"XPlat Code Coverage" `
        --results-directory "$coverageDir" `
        --logger "trx;LogFileName=test-results.trx" `
        --verbosity normal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    
    Write-Host "Tests completed successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Find coverage JSON file
    $coverageFiles = Get-ChildItem -Path $coverageDir -Recurse -Filter "coverage.json"
    
    if ($coverageFiles.Count -eq 0) {
        Write-Host "Coverage file not found. Trying alternative locations..." -ForegroundColor Yellow
        $coverageFiles = Get-ChildItem -Path "." -Recurse -Filter "coverage.json" | 
            Where-Object { $_.FullName -like "*TestResults*" -or $_.FullName -like "*coverage*" } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }
    
    if ($coverageFiles.Count -gt 0) {
        $coverageFile = $coverageFiles[0].FullName
        Write-Host "Found coverage file: $coverageFile" -ForegroundColor Green
        
        # Parse coverage data
        Write-Host ""
        Write-Host "Step 2/4: Parsing coverage data..." -ForegroundColor Cyan
        $coverageData = Get-Content $coverageFile | ConvertFrom-Json
        
        # Extract summary statistics
        $metrics = $coverageData.metrics
        $summary = @{
            Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            LinesCovered = $metrics.lines_covered
            LinesTotal = $metrics.lines_total
            LinesPercent = [math]::Round(($metrics.lines_covered / $metrics.lines_total) * 100, 2)
            BranchesCovered = $metrics.branches_covered
            BranchesTotal = $metrics.branches_total
            BranchesPercent = if ($metrics.branches_total -gt 0) { 
                [math]::Round(($metrics.branches_covered / $metrics.branches_total) * 100, 2) 
            } else { 
                0 
            }
            MethodsCovered = $metrics.methods_covered
            MethodsTotal = $metrics.methods_total
            MethodsPercent = [math]::Round(($metrics.methods_covered / $metrics.methods_total) * 100, 2)
        }
        
        Write-Host "Coverage Summary:" -ForegroundColor Cyan
        Write-Host "  Lines:     $($summary.LinesCovered)/$($summary.LinesTotal) ($($summary.LinesPercent)%)" -ForegroundColor White
        Write-Host "  Branches:  $($summary.BranchesCovered)/$($summary.BranchesTotal) ($($summary.BranchesPercent)%)" -ForegroundColor White
        Write-Host "  Methods:   $($summary.MethodsCovered)/$($summary.MethodsTotal) ($($summary.MethodsPercent)%)" -ForegroundColor White
        Write-Host ""
        
        # Save summary JSON
        $summaryPath = "$coverageDir/coverage-summary.json"
        $summary | ConvertTo-Json -Depth 10 | Out-File -FilePath $summaryPath
        Write-Host "Saved summary to: $summaryPath" -ForegroundColor Green
        
        # Analyze by module
        Write-Host ""
        Write-Host "Step 3/4: Analyzing coverage by module..." -ForegroundColor Cyan
        $moduleCoverage = @()
        
        foreach ($module in $coverageData.modules) {
            $moduleName = $module.name
            $moduleMetrics = $module.metrics
            
            if ($moduleMetrics.lines_total -gt 0) {
                $moduleCoverage += [PSCustomObject]@{
                    Module = $moduleName
                    LinesCovered = $moduleMetrics.lines_covered
                    LinesTotal = $moduleMetrics.lines_total
                    CoveragePercent = [math]::Round(($moduleMetrics.lines_covered / $moduleMetrics.lines_total) * 100, 2)
                    BranchesCovered = $moduleMetrics.branches_covered
                    BranchesTotal = $moduleMetrics.branches_total
                }
            }
        }
        
        $moduleCoverage = $moduleCoverage | Sort-Object CoveragePercent -Descending
        $moduleCoveragePath = "$coverageDir/module-coverage.csv"
        $moduleCoverage | Export-Csv -Path $moduleCoveragePath -NoTypeInformation
        Write-Host "Saved module coverage to: $moduleCoveragePath" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "Top 10 Modules by Coverage:" -ForegroundColor Cyan
        $moduleCoverage | Select-Object -First 10 | Format-Table -AutoSize
        
        Write-Host ""
        Write-Host "Bottom 10 Modules by Coverage:" -ForegroundColor Cyan
        $moduleCoverage | Select-Object -Last 10 | Format-Table -AutoSize
        
        # Identify gaps
        Write-Host ""
        Write-Host "Step 4/4: Identifying coverage gaps..." -ForegroundColor Cyan
        $uncoveredModules = $moduleCoverage | Where-Object { $_.CoveragePercent -eq 0 }
        $lowCoverageModules = $moduleCoverage | Where-Object { $_.CoveragePercent -gt 0 -and $_.CoveragePercent -lt 30 }
        
        $gapsPath = "$coverageDir/coverage-gaps.txt"
        "Coverage Gap Analysis - Generated $(Get-Date)" | Out-File -FilePath $gapsPath
        "" | Out-File -FilePath $gapsPath -Append
        "UNCovered Modules ($($uncoveredModules.Count)):" | Out-File -FilePath $gapsPath -Append
        $uncoveredModules | ForEach-Object { "  - $($_.Module): $($_.LinesTotal) lines" } | Out-File -FilePath $gapsPath -Append
        "" | Out-File -FilePath $gapsPath -Append
        "LOW Coverage Modules ($($lowCoverageModules.Count)):" | Out-File -FilePath $gapsPath -Append
        $lowCoverageModules | ForEach-Object { "  - $($_.Module): $($_.CoveragePercent)% coverage" } | Out-File -FilePath $gapsPath -Append
        
        Write-Host "Uncovered modules: $($uncoveredModules.Count)" -ForegroundColor Red
        Write-Host "Low coverage modules (<30%): $($lowCoverageModules.Count)" -ForegroundColor Yellow
        Write-Host "Saved gaps analysis to: $gapsPath" -ForegroundColor Green
        
        # Create latest symlink/copy
        $latestDir = "$OutputPath/latest"
        if (Test-Path $latestDir) {
            Remove-Item -Path $latestDir -Recurse -Force
        }
        Copy-Item -Path $coverageDir -Destination $latestDir -Recurse
        Write-Host ""
        Write-Host "Updated latest symlink to: $coverageDir" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "  Baseline Analysis Complete!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Results saved to: $coverageDir" -ForegroundColor White
        Write-Host "Summary: $summaryPath" -ForegroundColor White
        Write-Host "Modules: $moduleCoveragePath" -ForegroundColor White
        Write-Host "Gaps: $gapsPath" -ForegroundColor White
        Write-Host ""
        
        # Return data for further processing
        return $summary
    } else {
        Write-Host "No coverage files found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}

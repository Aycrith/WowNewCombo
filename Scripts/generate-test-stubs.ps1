#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Phase 3: Coverage Gap Analyzer - Self-Improving Test Generation
.DESCRIPTION
    Parses Cobertura coverage reports to find uncovered methods/classes
    and automatically generates test stubs for them.
.PARAMETER CoveragePath
    Path to the coverage.cobertura.xml file
.PARAMETER OutputPath
    Path to write the generated test stubs
.PARAMETER Threshold
    Coverage threshold below which to generate tests (default: 0%)
.PARAMETER MaxStubsPerClass
    Maximum number of test stubs to generate per class (default: 20)
.EXAMPLE
    .\generate-test-stubs.ps1 -CoveragePath "coverage.cobertura.xml" -OutputPath "..\CoreUnitTests\Generated"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$CoveragePath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    
    [int]$Threshold = 0,
    
    [int]$MaxStubsPerClass = 20
)

# Error handling
$ErrorActionPreference = "Stop"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Parse-CoverageReport {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        throw "Coverage file not found: $Path"
    }
    
    Write-ColorOutput "Parsing coverage report..." "Yellow"
    $xml = [xml](Get-Content $Path)
    $gaps = @()
    $totalClasses = 0
    $uncoveredClasses = 0
    $totalMethods = 0
    $uncoveredMethods = 0
    
    foreach ($package in $xml.coverage.packages.package) {
        $packageName = $package.name
        
        foreach ($class in $package.classes.class) {
            $totalClasses++
            $className = $class.name
            $fileName = $class.filename
            $lineRate = [decimal]$class.'line-rate'
            $branchRate = [decimal]$class.'branch-rate'
            
            # Count all methods
            foreach ($method in $class.methods.method) {
                $totalMethods++
            }
            
            # Skip if coverage is above threshold
            if ($lineRate -gt ($Threshold / 100)) {
                continue
            }
            
            $uncoveredClasses++
            $gap = [PSCustomObject]@{
                Package = $packageName
                Class = $className
                File = $fileName
                LineRate = $lineRate
                BranchRate = $branchRate
                UncoveredMethods = @()
            }
            
            foreach ($method in $class.methods.method) {
                $methodLineRate = [decimal]$method.'line-rate'
                if ($methodLineRate -eq 0) {
                    $uncoveredMethods++
                    $gap.UncoveredMethods += [PSCustomObject]@{
                        Name = $method.name
                        Signature = $method.signature
                        LineRate = $methodLineRate
                        LineNumber = $method.line
                    }
                }
            }
            
            if ($gap.UncoveredMethods.Count -gt 0) {
                $gaps += $gap
            }
        }
    }
    
    Write-ColorOutput "Parsed $totalClasses classes with $totalMethods methods" "Gray"
    Write-ColorOutput "Found $uncoveredClasses classes and $uncoveredMethods methods with 0% coverage" "Yellow"
    
    return $gaps
}

function Get-MethodParameters {
    param([string]$Signature)
    
    # Parse method signature like "(System.Int32,System.String)"
    if ($Signature -match '^\((.*)\)$') {
        $params = $matches[1] -split ',' | Where-Object { $_ -ne '' }
        return $params
    }
    return @()
}

function Get-TypeDefaultValue {
    param([string]$TypeName)
    
    switch -Regex ($TypeName) {
        '^System.Boolean$|^bool$' { return 'false' }
        '^System.Int32$|^int$' { return '0' }
        '^System.Int64$|^long$' { return '0L' }
        '^System.Single$|^float$' { return '0.0f' }
        '^System.Double$|^double$' { return '0.0' }
        '^System.String$|^string$' { return '""' }
        '^System.Char$|^char$' { return "'\\0'" }
        '^System.DateTime$' { return 'DateTime.MinValue' }
        '^System.TimeSpan$' { return 'TimeSpan.Zero' }
        '^System.Guid$' { return 'Guid.Empty' }
        '^System.Collections.Generic.List' { return 'new()' }
        '^System.Collections.Generic.Dictionary' { return 'new()' }
        default { return 'null' }
    }
}

function Convert-ToPascalCase {
    param([string]$Name)
    
    # Remove invalid characters for method names
    $cleanName = $Name -replace '[<>]', '' -replace '`\d+', '' -replace '\.', '_'
    
    if ($cleanName -match '^get_') {
        $base = $cleanName -replace '^get_', ''
        return 'Get' + (Get-Culture).TextInfo.ToTitleCase($base)
    }
    elseif ($cleanName -match '^set_') {
        $base = $cleanName -replace '^set_', ''
        return 'Set' + (Get-Culture).TextInfo.ToTitleCase($base)
    }
    else {
        return (Get-Culture).TextInfo.ToTitleCase($cleanName)
    }
}

function Generate-TestStub {
    param(
        [string]$Namespace,
        [string]$ClassName,
        [array]$Methods,
        [string]$OutputDir,
        [int]$MaxStubs
    )
    
    $testClassName = "$ClassName`Tests"
    $fileName = "$testClassName.cs"
    $filePath = Join-Path $OutputDir $fileName
    
    $sb = New-Object System.Text.StringBuilder
    
    # Add using statements
    [void]$sb.AppendLine("using FluentAssertions;")
    [void]$sb.AppendLine("using System;")
    [void]$sb.AppendLine("using System.Collections.Generic;")
    [void]$sb.AppendLine("using Xunit;")
    [void]$sb.AppendLine()
    
    # Add namespace
    $testNamespace = $Namespace -replace '^Core', 'CoreUnitTests'
    [void]$sb.AppendLine("namespace $testNamespace;")
    [void]$sb.AppendLine()
    
    # Add class documentation
    [void]$sb.AppendLine("/// <summary>")
    [void]$sb.AppendLine("/// Generated test suite for $ClassName")
    [void]$sb.AppendLine("/// Coverage: 0% - Auto-generated stub")
    [void]$sb.AppendLine("/// </summary>")
    [void]$sb.AppendLine("public class $testClassName")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine()
    
    # Limit methods to generate
    $methodsToGenerate = $Methods | Select-Object -First $MaxStubs
    
    # Generate test methods
    $methodCounter = 0
    foreach ($method in $methodsToGenerate) {
        $methodCounter++
        $originalName = $method.Name
        $methodName = Convert-ToPascalCase -Name $originalName
        $params = Get-MethodParameters -Signature $method.Signature
        
        [void]$sb.AppendLine("    #region $methodName ($methodCounter)")
        [void]$sb.AppendLine()
        
        # Happy path test
        [void]$sb.AppendLine("    [Fact]")
        [void]$sb.AppendLine("    public void ${methodName}_HappyPath_ReturnsExpected()")
        [void]$sb.AppendLine("    {")
        [void]$sb.AppendLine("        // Arrange")
        
        # Generate arrange section
        if ($methodName -match '^Get') {
            [void]$sb.AppendLine("        // TODO: Setup instance")
            [void]$sb.AppendLine("        var instance = new $ClassName();")
        }
        elseif ($methodName -match '^Set') {
            [void]$sb.AppendLine("        // TODO: Setup instance and value")
            [void]$sb.AppendLine("        var instance = new $ClassName();")
            [void]$sb.AppendLine("        var value = default; // Replace with actual type")
        }
        else {
            [void]$sb.AppendLine("        // TODO: Setup test dependencies")
            [void]$sb.AppendLine("        var instance = new $ClassName();")
            
            if ($params.Count -gt 0) {
                [void]$sb.AppendLine()
                [void]$sb.AppendLine("        // Parameters:")
                $paramIndex = 0
                foreach ($param in $params) {
                    $paramIndex++
                    $defaultValue = Get-TypeDefaultValue -TypeName $param
                    [void]$sb.AppendLine("        // param$paramIndex = $defaultValue; // $param")
                }
            }
        }
        
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("        // Act")
        [void]$sb.AppendLine("        // TODO: Call $originalName")
        [void]$sb.AppendLine("        var result = true;")
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("        // Assert")
        [void]$sb.AppendLine("        // TODO: Verify expected behavior")
        [void]$sb.AppendLine("        result.Should().BeTrue();")
        [void]$sb.AppendLine("    }")
        [void]$sb.AppendLine()
        
        # Edge case test (only for methods with parameters)
        if ($params.Count -gt 0) {
            [void]$sb.AppendLine("    [Fact]")
            [void]$sb.AppendLine("    public void ${methodName}_InvalidInput_HandlesGracefully()")
            [void]$sb.AppendLine("    {")
            [void]$sb.AppendLine("        // Arrange")
            [void]$sb.AppendLine("        // TODO: Setup invalid input scenario")
            [void]$sb.AppendLine("        var instance = new $ClassName();")
            [void]$sb.AppendLine()
            [void]$sb.AppendLine("        // Act & Assert")
            [void]$sb.AppendLine("        // TODO: Verify exception handling or error case")
            [void]$sb.AppendLine("        // Assert.Throws<Exception>(() => instance.$originalName());")
            [void]$sb.AppendLine("    }")
            [void]$sb.AppendLine()
        }
        
        [void]$sb.AppendLine("    #endregion")
        [void]$sb.AppendLine()
    }
    
    if ($Methods.Count -gt $MaxStubs) {
        [void]$sb.AppendLine("    // NOTE: Only first $MaxStubs of $($Methods.Count) methods generated")
        [void]$sb.AppendLine("    // Add more tests manually or increase MaxStubsPerClass")
        [void]$sb.AppendLine()
    }
    
    # Close class
    [void]$sb.AppendLine("}")
    
    # Write file
    $testCode = $sb.ToString()
    Set-Content -Path $filePath -Value $testCode -Encoding UTF8
    
    return $filePath
}

function Generate-CoverageReport {
    param([array]$Gaps, [string]$ReportPath, [int]$TotalUncovered)
    
    $sb = New-Object System.Text.StringBuilder
    
    [void]$sb.AppendLine("# Phase 3: Coverage Gap Analysis Report")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Executive Summary")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Metric | Value |")
    [void]$sb.AppendLine("|--------|-------|")
    [void]$sb.AppendLine("| Classes with gaps | $($Gaps.Count) |")
    [void]$sb.AppendLine("| Total uncovered methods | $TotalUncovered |")
    [void]$sb.AppendLine("| Test stubs generated | $($Gaps | ForEach-Object { [Math]::Min($_.UncoveredMethods.Count, $MaxStubsPerClass) } | Measure-Object -Sum | Select-Object -ExpandProperty Sum) |")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Coverage Gaps by Priority")
    [void]$sb.AppendLine("")
    
    $priority = 1
    foreach ($gap in $Gaps | Sort-Object LineRate) {
        [void]$sb.AppendLine("### Priority $priority`: $($gap.Class)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("- **Package:** $($gap.Package)")
        [void]$sb.AppendLine("- **File:** $($gap.File)")
        [void]$sb.AppendLine("- **Line Coverage:** $([math]::Round($gap.LineRate * 100, 2))%")
        [void]$sb.AppendLine("- **Branch Coverage:** $([math]::Round($gap.BranchRate * 100, 2))%")
        [void]$sb.AppendLine("- **Uncovered Methods:** $($gap.UncoveredMethods.Count)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("#### Top Uncovered Methods")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| # | Method | Line |")
        [void]$sb.AppendLine("|---|--------|------|")
        
        $methodNum = 0
        foreach ($method in $gap.UncoveredMethods | Select-Object -First $MaxStubsPerClass) {
            $methodNum++
            [void]$sb.AppendLine("| $methodNum | ``$($method.Name)`` | $($method.LineNumber) |")
        }
        
        if ($gap.UncoveredMethods.Count -gt $MaxStubsPerClass) {
            [void]$sb.AppendLine("| ... | *and $($gap.UncoveredMethods.Count - $MaxStubsPerClass) more* | |")
        }
        
        [void]$sb.AppendLine("")
        $priority++
    }
    
    [void]$sb.AppendLine("## Implementation Checklist")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- [ ] Review generated test stubs")
    [void]$sb.AppendLine("- [ ] Add proper test data and mocks")
    [void]$sb.AppendLine("- [ ] Implement happy path tests")
    [void]$sb.AppendLine("- [ ] Add edge case and error handling tests")
    [void]$sb.AppendLine("- [ ] Run tests and verify coverage improvement")
    [void]$sb.AppendLine("- [ ] Refactor tests for clarity and maintainability")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Recommendations")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("1. **Start with Priority 1** classes - they have the most significant coverage gaps")
    [void]$sb.AppendLine("2. **Focus on public methods** - internal/private methods can be tested via public API")
    [void]$sb.AppendLine("3. **Use MockWoWClient** for integration-style tests")
    [void]$sb.AppendLine("4. **Property-based testing** - consider using FsCheck for complex input validation")
    [void]$sb.AppendLine("5. **Document complex logic** - add comments explaining business rules in tests")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("---")
    [void]$sb.AppendLine("*Auto-generated by Phase 3: Self-Improving Test Generation*")
    
    Set-Content -Path $ReportPath -Value $sb.ToString() -Encoding UTF8
}

# Main execution
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput " Phase 3: Self-Improving Test Generation" "Cyan"
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput ""

# Validate inputs
if (-not (Test-Path $CoveragePath)) {
    Write-ColorOutput "ERROR: Coverage file not found: $CoveragePath" "Red"
    exit 1
}

# Create output directory if it doesn't exist
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-ColorOutput "Created output directory: $OutputPath" "Green"
}

# Parse coverage report
$gaps = Parse-CoverageReport -Path $CoveragePath
Write-ColorOutput ""

if ($gaps.Count -eq 0) {
    Write-ColorOutput "✓ No coverage gaps found above threshold!" "Green"
    exit 0
}

# Generate test stubs
$generatedFiles = @()
$generatedTestCount = 0
$totalUncoveredMethods = 0

foreach ($gap in $gaps) {
    $totalUncoveredMethods += $gap.UncoveredMethods.Count
    $namespace = $gap.Package
    $className = ($gap.Class -split '\.')[-1]
    
    Write-ColorOutput "Generating: $className ($($gap.UncoveredMethods.Count) methods, $([math]::Round($gap.LineRate * 100, 1))% coverage)" "Yellow"
    
    try {
        $filePath = Generate-TestStub `
            -Namespace $namespace `
            -ClassName $className `
            -Methods $gap.UncoveredMethods `
            -OutputDir $OutputPath `
            -MaxStubs $MaxStubsPerClass
        
        $generatedFiles += $filePath
        $stubsGenerated = [Math]::Min($gap.UncoveredMethods.Count, $MaxStubsPerClass)
        $generatedTestCount += $stubsGenerated * 2  # Happy path + edge case
        
        Write-ColorOutput "  → Created: $(Split-Path $filePath -Leaf) ($stubsGenerated method stubs)" "Green"
    }
    catch {
        Write-ColorOutput "  ✗ Failed to generate for $className`: $_" "Red"
    }
}

Write-ColorOutput ""
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput " Generation Summary" "Cyan"
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput "Files generated: $($generatedFiles.Count)" "White"
Write-ColorOutput "Test stubs created: ~$generatedTestCount" "White"
Write-ColorOutput "Total uncovered methods: $totalUncoveredMethods" "White"
Write-ColorOutput ""

# Generate markdown report
$reportPath = Join-Path $OutputPath "coverage-gaps-report.md"
Generate-CoverageReport -Gaps $gaps -ReportPath $reportPath -TotalUncovered $totalUncoveredMethods
Write-ColorOutput "Report saved to: $reportPath" "Green"

Write-ColorOutput ""
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput " Phase 3 Complete!" "Cyan"
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput ""
Write-ColorOutput "Next steps:" "Yellow"
Write-ColorOutput "  1. Review generated stubs in: $OutputPath" "White"
Write-ColorOutput "  2. Implement actual test logic" "White"
Write-ColorOutput "  3. Run tests to verify coverage improvement" "White"
Write-ColorOutput "  4. Refactor for clarity" "White"

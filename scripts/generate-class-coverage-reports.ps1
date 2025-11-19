#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates detailed per-class coverage reports from Cobertura XML.

.DESCRIPTION
    Parses the Cobertura coverage XML file and generates individual text reports
    for each class with detailed coverage metrics. Also outputs a summary to the console.

.PARAMETER CoberturaXmlPath
    Path to the coverage.cobertura.xml file

.PARAMETER OutputDir
    Directory where class coverage reports will be written

.PARAMETER Threshold
    Coverage threshold percentage for highlighting (default: 70)

.EXAMPLE
    .\scripts\generate-class-coverage-reports.ps1 -CoberturaXmlPath "coverage\TestResults\coverage.cobertura.xml" -OutputDir "coverage\report"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$CoberturaXmlPath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputDir,
    
    [int]$Threshold = 70
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CoberturaXmlPath)) {
    Write-Error "Cobertura XML file not found: $CoberturaXmlPath"
    exit 1
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Parse the XML
Write-Host "Parsing coverage data from: $CoberturaXmlPath" -ForegroundColor Cyan
[xml]$coverage = Get-Content $CoberturaXmlPath

# Calculate overall coverage
$overallLineRate = [math]::Round([double]$coverage.coverage.'line-rate' * 100, 1)
$overallBranchRate = [math]::Round([double]$coverage.coverage.'branch-rate' * 100, 1)

# Collect all classes with their coverage data
$classData = @()

foreach ($package in $coverage.coverage.packages.package) {
    foreach ($class in $package.classes.class) {
        $className = $class.name
        
        # Skip generated views and Program class (not meaningfully testable)
        if ($className -like "*AspNetCoreGeneratedDocument*" -or $className -eq "Program") {
            continue
        }
        
        # Extract just the class name without namespace
        $shortClassName = $className.Split('.')[-1]
        $namespace = $className.Substring(0, [Math]::Max(0, $className.LastIndexOf('.')))
        
        # Calculate metrics
        $lineRate = [math]::Round([double]$class.'line-rate' * 100, 1)
        $branchRate = if ($class.'branch-rate') { [math]::Round([double]$class.'branch-rate' * 100, 1) } else { 0 }
        
        # Count lines
        $lines = $class.lines.line
        $totalLines = @($lines).Count
        $coveredLines = @($lines | Where-Object { $_.hits -gt 0 }).Count
        $uncoveredLines = $totalLines - $coveredLines
        
        # Count branches
        $totalBranches = 0
        $coveredBranches = 0
        foreach ($line in $lines) {
            if ($line.'condition-coverage') {
                # Parse condition-coverage like "50% (1/2)"
                if ($line.'condition-coverage' -match '\((\d+)/(\d+)\)') {
                    $coveredBranches += [int]$matches[1]
                    $totalBranches += [int]$matches[2]
                }
            }
        }
        
        # Count methods
        $methods = $class.methods.method
        $totalMethods = @($methods).Count
        $coveredMethods = 0
        $fullyCoveredMethods = 0
        
        foreach ($method in $methods) {
            $methodLines = $method.lines.line
            $methodCoveredLines = @($methodLines | Where-Object { $_.hits -gt 0 }).Count
            if ($methodCoveredLines -gt 0) {
                $coveredMethods++
                if ($methodCoveredLines -eq @($methodLines).Count) {
                    $fullyCoveredMethods++
                }
            }
        }
        
        # Find uncovered methods
        $uncoveredMethods = @()
        foreach ($method in $methods) {
            $methodLines = $method.lines.line
            $methodCoveredLines = @($methodLines | Where-Object { $_.hits -gt 0 }).Count
            if ($methodCoveredLines -eq 0) {
                $methodName = $method.name
                # Clean up method name (remove parameter signatures for readability)
                if ($methodName -match '^([^(]+)') {
                    $methodName = $matches[1]
                }
                $uncoveredMethods += $methodName
            }
        }
        
        # Find partially covered methods (good candidates for improvement)
        $partiallyCoveredMethods = @()
        foreach ($method in $methods) {
            $methodLines = $method.lines.line
            $methodTotalLines = @($methodLines).Count
            $methodCoveredLines = @($methodLines | Where-Object { $_.hits -gt 0 }).Count
            if ($methodCoveredLines -gt 0 -and $methodCoveredLines -lt $methodTotalLines) {
                $methodName = $method.name
                if ($methodName -match '^([^(]+)') {
                    $methodName = $matches[1]
                }
                $methodCoverage = [math]::Round(($methodCoveredLines / $methodTotalLines) * 100, 0)
                $partiallyCoveredMethods += [PSCustomObject]@{
                    Name = $methodName
                    Coverage = $methodCoverage
                    Covered = $methodCoveredLines
                    Total = $methodTotalLines
                }
            }
        }
        
        $classInfo = [PSCustomObject]@{
            ClassName = $shortClassName
            FullName = $className
            Namespace = $namespace
            LineRate = $lineRate
            BranchRate = $branchRate
            TotalLines = $totalLines
            CoveredLines = $coveredLines
            UncoveredLines = $uncoveredLines
            TotalBranches = $totalBranches
            CoveredBranches = $coveredBranches
            TotalMethods = $totalMethods
            CoveredMethods = $coveredMethods
            FullyCoveredMethods = $fullyCoveredMethods
            UncoveredMethods = $uncoveredMethods
            PartiallyCoveredMethods = $partiallyCoveredMethods
        }
        
        $classData += $classInfo
    }
}

# Sort by line coverage (lowest first to highlight problem areas)
$classData = $classData | Sort-Object LineRate

# Generate individual class reports
Write-Host ""
Write-Host "Generating individual class coverage reports..." -ForegroundColor Cyan

foreach ($class in $classData) {
    # Sanitize class name for file system (remove invalid characters)
    $sanitizedClassName = $class.ClassName -replace '[<>:"/\\|?*]', '_'
    $reportPath = Join-Path $OutputDir "ClassCoverage_$sanitizedClassName.txt"
    
    $report = @"
================================================================================
Class Coverage Report: $($class.ClassName)
================================================================================
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Full Name: $($class.FullName)
Namespace: $($class.Namespace)

COVERAGE SUMMARY
--------------------------------------------------------------------------------
Line Coverage:       $($class.LineRate)% ($($class.CoveredLines)/$($class.TotalLines) lines)
Branch Coverage:     $($class.BranchRate)% ($($class.CoveredBranches)/$($class.TotalBranches) branches)
Method Coverage:     $(if ($class.TotalMethods -gt 0) { [math]::Round(($class.CoveredMethods / $class.TotalMethods) * 100, 1) } else { 0 })% ($($class.CoveredMethods)/$($class.TotalMethods) methods)
Fully Covered:       $(if ($class.TotalMethods -gt 0) { [math]::Round(($class.FullyCoveredMethods / $class.TotalMethods) * 100, 1) } else { 0 })% ($($class.FullyCoveredMethods)/$($class.TotalMethods) methods)

STATUS
--------------------------------------------------------------------------------
"@

    if ($class.LineRate -ge $Threshold) {
        $report += "✓ MEETS THRESHOLD ($Threshold%)`n"
    } else {
        $report += "⚠ BELOW THRESHOLD ($Threshold%) - Needs $([math]::Round($Threshold - $class.LineRate, 1))% improvement`n"
    }
    
    $report += "`n"
    
    # Uncovered methods section
    if ($class.UncoveredMethods.Count -gt 0) {
        $report += @"
UNCOVERED METHODS ($($class.UncoveredMethods.Count))
--------------------------------------------------------------------------------
"@
        foreach ($method in $class.UncoveredMethods) {
            $report += "  • $method`n"
        }
        $report += "`n"
    }
    
    # Partially covered methods section
    if ($class.PartiallyCoveredMethods.Count -gt 0) {
        $report += @"
PARTIALLY COVERED METHODS ($($class.PartiallyCoveredMethods.Count))
--------------------------------------------------------------------------------
"@
        foreach ($method in ($class.PartiallyCoveredMethods | Sort-Object Coverage)) {
            $report += "  • $($method.Name): $($method.Coverage)% ($($method.Covered)/$($method.Total) lines)`n"
        }
        $report += "`n"
    }
    
    # Recommendations
    $report += @"
RECOMMENDATIONS
--------------------------------------------------------------------------------
"@
    
    if ($class.LineRate -ge 90) {
        $report += "Excellent coverage! Consider adding edge case tests.`n"
    } elseif ($class.LineRate -ge $Threshold) {
        $report += "Good coverage. Focus on uncovered branches and edge cases.`n"
    } elseif ($class.LineRate -ge 50) {
        $report += "Moderate coverage. Add tests for uncovered methods and branches.`n"
    } else {
        $report += "Low coverage. Prioritize adding tests for this class.`n"
    }
    
    if ($class.UncoveredMethods.Count -gt 0) {
        $report += "Focus on the $($class.UncoveredMethods.Count) uncovered method(s) listed above.`n"
    }
    
    if ($class.PartiallyCoveredMethods.Count -gt 0) {
        $report += "Improve coverage of $($class.PartiallyCoveredMethods.Count) partially covered method(s).`n"
    }
    
    if ($class.TotalBranches -gt 0 -and $class.BranchRate -lt $class.LineRate) {
        $report += "Branch coverage is lower than line coverage - add tests for conditional logic.`n"
    }
    
    $report += @"

For detailed line-by-line coverage, see: Quintessentia_$($class.ClassName).html

================================================================================
"@
    
    Set-Content -Path $reportPath -Value $report -Encoding UTF8
}

# Display console summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Code Coverage by Class" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Overall Coverage: $overallLineRate% lines, $overallBranchRate% branches" -ForegroundColor Yellow
Write-Host ""

foreach ($class in ($classData | Sort-Object LineRate -Descending)) {
    $symbol = if ($class.LineRate -ge $Threshold) { "✓" } else { "⚠" }
    $color = if ($class.LineRate -ge $Threshold) { "Green" } else { "Yellow" }
    
    $className = $class.ClassName.PadRight(35)
    $lineInfo = "$($class.LineRate)%".PadLeft(6)
    $detailInfo = "($($class.CoveredLines)/$($class.TotalLines) lines"
    
    if ($class.TotalBranches -gt 0) {
        $detailInfo += ", $($class.CoveredBranches)/$($class.TotalBranches) branches"
    }
    $detailInfo += ")"
    
    Write-Host "$symbol " -NoNewline -ForegroundColor $color
    Write-Host "$className" -NoNewline -ForegroundColor White
    Write-Host "$lineInfo " -NoNewline -ForegroundColor $color
    Write-Host "$detailInfo" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Detailed reports: " -NoNewline -ForegroundColor Cyan
Write-Host "$OutputDir\ClassCoverage_*.txt" -ForegroundColor White
Write-Host "HTML report:      " -NoNewline -ForegroundColor Cyan
Write-Host "$OutputDir\index.html" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "Class coverage reports generated successfully!" -ForegroundColor Green

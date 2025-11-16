#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Opens the most recent code coverage HTML report.

.DESCRIPTION
    This script opens the latest generated code coverage HTML report in the default browser.
    If no report exists, it prompts to generate one.

.EXAMPLE
    .\scripts\view-coverage.ps1
    Opens the coverage report in the default browser
#>

$ErrorActionPreference = "Stop"

# Get script and solution paths
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Split-Path -Parent $scriptPath
$reportPath = Join-Path $solutionRoot "coverage\report\index.html"

Write-Host "Quintessentia Code Coverage Viewer" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

if (Test-Path $reportPath) {
    Write-Host "Opening coverage report..." -ForegroundColor Green
    Write-Host "Report: $reportPath" -ForegroundColor Cyan
    Start-Process $reportPath
} else {
    Write-Host "No coverage report found at: $reportPath" -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Would you like to generate a coverage report now? (Y/N)"
    
    if ($response -eq "Y" -or $response -eq "y") {
        $generateScript = Join-Path $scriptPath "generate-coverage.ps1"
        & $generateScript
    } else {
        Write-Host ""
        Write-Host "To generate a coverage report, run:" -ForegroundColor Yellow
        Write-Host "  .\scripts\generate-coverage.ps1" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Or build the solution in Debug mode:" -ForegroundColor Yellow
        Write-Host "  dotnet build" -ForegroundColor Cyan
    }
}

#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Wrapper script to find the most recent coverage file and generate class reports.

.PARAMETER CoverageOutputDir
    Base coverage output directory

.PARAMETER Threshold
    Coverage threshold percentage
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$CoverageOutputDir,
    
    [Parameter(Mandatory=$true)]
    [string]$ScriptDir,
    
    [int]$Threshold = 70
)

$ErrorActionPreference = "Stop"

# Find the most recent coverage.cobertura.xml file
$testResultsDir = Join-Path $CoverageOutputDir "TestResults"
$coverageFiles = Get-ChildItem -Path $testResultsDir -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue

if ($coverageFiles.Count -eq 0) {
    Write-Host "No coverage files found in $testResultsDir" -ForegroundColor Yellow
    exit 0
}

$mostRecentFile = $coverageFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host "Using coverage file: $($mostRecentFile.FullName)" -ForegroundColor Cyan

# Call the main script
$mainScript = Join-Path $ScriptDir "generate-class-coverage-reports.ps1"
& $mainScript -CoberturaXmlPath $mostRecentFile.FullName -OutputDir (Join-Path $CoverageOutputDir "report") -Threshold $Threshold

exit $LASTEXITCODE

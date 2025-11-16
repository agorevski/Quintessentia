#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates code coverage reports for the Quintessentia project.

.DESCRIPTION
    This script runs all tests with code coverage collection, generates detailed HTML reports,
    and displays coverage statistics. It uses Coverlet for coverage collection and ReportGenerator
    for HTML report generation.

.PARAMETER Threshold
    Minimum code coverage threshold percentage (default: 70)

.PARAMETER FailOnThreshold
    Whether to fail if coverage is below threshold (default: false)

.PARAMETER OpenReport
    Whether to automatically open the HTML report after generation (default: true)

.EXAMPLE
    .\scripts\generate-coverage.ps1
    Generates coverage report with default settings

.EXAMPLE
    .\scripts\generate-coverage.ps1 -Threshold 80 -FailOnThreshold $true
    Generates report with 80% threshold and fails if below threshold
#>

param(
    [int]$Threshold = 70,
    [bool]$FailOnThreshold = $false,
    [bool]$OpenReport = $true
)

$ErrorActionPreference = "Stop"

# Get script and solution paths
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Split-Path -Parent $scriptPath
$coverageDir = Join-Path $solutionRoot "coverage"
$testProject = Join-Path $solutionRoot "tests\Quintessentia.Tests\Quintessentia.Tests.csproj"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Quintessentia Code Coverage Report" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure coverage directory exists
if (Test-Path $coverageDir) {
    Write-Host "Cleaning existing coverage directory..." -ForegroundColor Yellow
    Remove-Item -Path $coverageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null

Write-Host "Running tests with coverage collection..." -ForegroundColor Green
Write-Host "Coverage threshold: $Threshold%" -ForegroundColor Green
Write-Host ""

# Run tests with coverage
$coverageArgs = @(
    "test"
    $testProject
    "--configuration", "Debug"
    "/p:CollectCoverage=true"
    "/p:CoverletOutputFormat=cobertura,json"
    "/p:CoverletOutput=$coverageDir\"
    "/p:Threshold=$Threshold"
    "/p:ThresholdType=line"
    "/p:Exclude=[*.Tests]*,[*]*.Program,[*]*.Views.*,[*]*.wwwroot.*"
    "/p:ExcludeByFile=**\*.cshtml,**\*.css,**\*.js"
)

if ($FailOnThreshold) {
    $coverageArgs += "/p:ThresholdStat=total"
}

$testResult = & dotnet @coverageArgs
$testExitCode = $LASTEXITCODE

if ($testExitCode -ne 0 -and $FailOnThreshold) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR: Tests failed or coverage below threshold!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit $testExitCode
}

Write-Host ""
Write-Host "Installing/Updating ReportGenerator tool..." -ForegroundColor Green
dotnet tool install --global dotnet-reportgenerator-globaltool --ignore-failed-sources 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    dotnet tool update --global dotnet-reportgenerator-globaltool --ignore-failed-sources 2>&1 | Out-Null
}

Write-Host "Generating HTML coverage report..." -ForegroundColor Green
$reportDir = Join-Path $coverageDir "report"

# Find the most recent coverage file
$coberturaFiles = Get-ChildItem -Path $coverageDir -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue
if ($coberturaFiles.Count -eq 0) {
    $coberturaFiles = Get-ChildItem -Path (Join-Path $solutionRoot "tests\Quintessentia.Tests\TestResults") -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue
}

$coberturaFile = if ($coberturaFiles.Count -gt 0) { 
    ($coberturaFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName 
} else { 
    $null 
}

if ($coberturaFile -and (Test-Path $coberturaFile)) {
    reportgenerator `
        -reports:$coberturaFile `
        -targetdir:$reportDir `
        -reporttypes:"Html;Badges" `
        -verbosity:Warning

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "Coverage Report Generated Successfully!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Report Location: $reportDir" -ForegroundColor Cyan
        Write-Host "HTML Report: $(Join-Path $reportDir 'index.html')" -ForegroundColor Cyan
        Write-Host ""

        # Parse coverage percentage from Cobertura XML
        [xml]$coverageXml = Get-Content $coberturaFile
        $lineRate = [math]::Round([double]$coverageXml.coverage.'line-rate' * 100, 2)
        $branchRate = [math]::Round([double]$coverageXml.coverage.'branch-rate' * 100, 2)

        Write-Host "Coverage Summary:" -ForegroundColor Yellow
        Write-Host "  Line Coverage:   $lineRate%" -ForegroundColor $(if ($lineRate -ge $Threshold) { "Green" } else { "Red" })
        Write-Host "  Branch Coverage: $branchRate%" -ForegroundColor Cyan
        Write-Host ""

        if ($OpenReport) {
            $indexPath = Join-Path $reportDir "index.html"
            Write-Host "Opening coverage report in browser..." -ForegroundColor Green
            Start-Process $indexPath
        }
    } else {
        Write-Host "ERROR: Failed to generate HTML report" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "ERROR: Coverage file not found at $coberturaFile" -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Coverage analysis complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

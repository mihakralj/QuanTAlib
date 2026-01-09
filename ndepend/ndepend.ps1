#!/usr/bin/env pwsh
#requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$NdependLicense = $env:NDEPEND_LICENSE
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Configuration
# Determine NDepend installation path based on OS
if ($IsWindows -or $env:OS -like "Windows*") {
    $NdependDll = "C:\ndepend\net10.0\NDepend.Console.MultiOS.dll"
} else {
    $NdependDll = Join-Path $HOME "NDepend/net10.0/NDepend.Console.MultiOS.dll"
}
$ScriptDir = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $ScriptDir
$CoverageDir = Join-Path $ScriptDir "coverage"
$SarifDir = Join-Path $ProjectRoot ".sarif"
$SolutionFile = Join-Path $ProjectRoot "QuanTAlib.sln"
$TestProject = Join-Path $ProjectRoot "lib/QuanTAlib.Tests.csproj"
$RunSettingsFile = Join-Path $ProjectRoot "coverlet.runsettings"
$NdependProject = Join-Path $ScriptDir "quantalib.ndproj"

# Validate prerequisites
if (-not (Test-Path $NdependDll)) {
    Write-Error "NDepend console not found at: $NdependDll"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($NdependLicense)) {
    Write-Warning "NDEPEND_LICENSE environment variable not set. License activation may fail."
}

if (-not (Test-Path $SolutionFile)) {
    Write-Error "Solution file not found: $SolutionFile"
    exit 1
}

# Helper function for section headers
function Write-Section {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

# Track analysis failure
$AnalysisFailed = $false

try {
    # Create .sarif directory
    Write-Section "Creating .sarif directory for Roslyn analyzer output"
    if (-not (Test-Path $SarifDir)) {
        New-Item -ItemType Directory -Path $SarifDir -Force | Out-Null
    }
    Write-Host "SARIF directory: $SarifDir"

    # Restore, clean, and build
    Write-Section "Cleaning and building solution (generates SARIF files)"
    
    Write-Host "Restoring packages..." -ForegroundColor Gray
    dotnet restore $SolutionFile -v q
    if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE" }

    Write-Host "Cleaning solution..." -ForegroundColor Gray
    dotnet clean $SolutionFile -v q
    if ($LASTEXITCODE -ne 0) { throw "Clean failed with exit code $LASTEXITCODE" }

    Write-Host "Removing old coverage data..." -ForegroundColor Gray
    if (Test-Path $CoverageDir) {
        Remove-Item -Path $CoverageDir -Recurse -Force
    }

    Write-Host "Building solution..." -ForegroundColor Gray
    dotnet build $SolutionFile -c Debug --no-incremental
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

    # Run tests with coverage
    Write-Section "Running tests with coverage"
    dotnet test $TestProject `
        -c Debug `
        --no-build `
        --collect:"XPlat Code Coverage" `
        --settings $RunSettingsFile `
        --results-directory:$CoverageDir
    
    if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE" }

    # Find coverage file
    Write-Host "`nSearching for coverage file..." -ForegroundColor Gray
    $CoverageFile = Get-ChildItem -Path $CoverageDir -Filter "coverage.opencover.xml" -Recurse -File | 
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $CoverageFile) {
        Write-Warning "Coverage file not found in $CoverageDir"
    } else {
        Write-Host "Coverage file: $CoverageFile" -ForegroundColor Green
    }

    # List SARIF files
    Write-Section "SARIF files generated"
    $SarifFiles = Get-ChildItem -Path $SarifDir -Filter "*.json" -ErrorAction SilentlyContinue
    if ($SarifFiles) {
        $SarifFiles | ForEach-Object {
            Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "No SARIF files found" -ForegroundColor Yellow
    }

    # Activate NDepend license
    Write-Section "Activating NDepend license"
    if (-not [string]::IsNullOrWhiteSpace($NdependLicense)) {
        dotnet $NdependDll --RegLic $NdependLicense
        if ($LASTEXITCODE -ne 0) { Write-Warning "License activation returned exit code $LASTEXITCODE" }
    } else {
        Write-Warning "Skipping license activation (no license provided)"
    }

    # Run NDepend analysis
    Write-Section "Running NDepend analysis"
    $NdependArgs = @($NdependProject)
    if ($CoverageFile) {
        $NdependArgs += "/CoverageFiles"
        $NdependArgs += $CoverageFile
    }

    dotnet $NdependDll @NdependArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "NDepend analysis completed with exit code $LASTEXITCODE"
        $AnalysisFailed = $true
    }

} catch {
    Write-Error "Script failed: $_"
    $AnalysisFailed = $true
} finally {
    # Always deactivate license
    Write-Section "Deactivating NDepend license"
    if (-not [string]::IsNullOrWhiteSpace($NdependLicense)) {
        dotnet $NdependDll --UnregLic
        if ($LASTEXITCODE -ne 0) { Write-Warning "License deactivation returned exit code $LASTEXITCODE" }
    } else {
        Write-Host "Skipping license deactivation (no license provided)" -ForegroundColor Gray
    }

    Write-Section "Done"
    
    if ($AnalysisFailed) {
        Write-Host "Note: Analysis completed with quality gate failures" -ForegroundColor Yellow
        exit 1
    }
}

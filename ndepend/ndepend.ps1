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
# Ensure ScriptDir is set correctly even if $PSScriptRoot is empty (e.g., dot-sourced)
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ProjectRoot = Split-Path -Parent $ScriptDir
$CoverageDir = Join-Path $ScriptDir "coverage"
$SarifDir = Join-Path $ProjectRoot ".sarif"
$SolutionFile = Join-Path $ProjectRoot "QuanTAlib.slnx"
$TestProject = Join-Path $ProjectRoot "lib/QuanTAlib.Tests.csproj"
$TestProject2 = Join-Path $ProjectRoot "quantower/Quantower.Tests.csproj"
$RunSettingsFile = Join-Path $ProjectRoot ".config/coverage.runsettings"
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
    Write-Information "`n=== $Message ==="
}

# Track analysis failure
$AnalysisFailed = $false

try {
    # Create .sarif directory
    Write-Section "Creating .sarif directory for Roslyn analyzer output"
    if (-not (Test-Path $SarifDir)) {
        New-Item -ItemType Directory -Path $SarifDir -Force | Out-Null
    }
    Write-Information "SARIF directory: $SarifDir"

    # Restore, clean, and build
    Write-Section "Cleaning and building solution (generates SARIF files)"
    
    Write-Information "Restoring packages..."
    dotnet restore $SolutionFile -v q
    if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE" }

    Write-Information "Cleaning solution..."
    dotnet clean $SolutionFile -v q
    if ($LASTEXITCODE -ne 0) { throw "Clean failed with exit code $LASTEXITCODE" }

    Write-Information "Removing old coverage data..."
    if (Test-Path $CoverageDir) {
        Remove-Item -Path $CoverageDir -Recurse -Force
    }

    Write-Information "Building solution..."
    dotnet build $SolutionFile -c Debug --no-incremental
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

    # Run tests with coverage (QuanTAlib.Tests)
    Write-Section "Running tests with coverage (QuanTAlib.Tests)"
    dotnet test $TestProject `
        -c Debug `
        --no-build `
        --collect:"XPlat Code Coverage" `
        --settings $RunSettingsFile `
        --results-directory:$CoverageDir
    
    if ($LASTEXITCODE -ne 0) { throw "Tests failed for QuanTAlib.Tests with exit code $LASTEXITCODE" }

    # Run tests with coverage (Quantower.Tests)
    Write-Section "Running tests with coverage (Quantower.Tests)"
    dotnet test $TestProject2 `
        -c Debug `
        --no-build `
        --collect:"XPlat Code Coverage" `
        --settings $RunSettingsFile `
        --results-directory:$CoverageDir
        
    if ($LASTEXITCODE -ne 0) { throw "Tests failed for Quantower.Tests with exit code $LASTEXITCODE" }

    # Find coverage files
    Write-Information "`nSearching for coverage files..."
    $CoverageFiles = Get-ChildItem -Path $CoverageDir -Filter "coverage.opencover.xml" -Recurse -File | 
    Select-Object -ExpandProperty FullName

    if (-not $CoverageFiles) {
        Write-Warning "No coverage files found in $CoverageDir"
    }
    else {
        $CoverageFiles | ForEach-Object {
            Write-Information "Coverage file: $_"
        }
    }

    # Run JetBrains InspectCode
    Write-Section "Running JetBrains InspectCode"
    $InspectCodeOutput = Join-Path $SarifDir "resharper.sarif.json"
    $jbPath = Get-Command "jb" -ErrorAction SilentlyContinue
    if ($jbPath) {
        Write-Information "Running InspectCode analysis..."
        jb inspectcode $SolutionFile --output=$InspectCodeOutput --format=Sarif --no-build
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "InspectCode completed with exit code $LASTEXITCODE"
        }
        else {
            Write-Information "InspectCode SARIF saved to: $InspectCodeOutput"
        }
    }
    else {
        Write-Warning "JetBrains CLI (jb) not found. Skipping InspectCode analysis."
        Write-Information "  Install with: dotnet tool install -g JetBrains.ReSharper.GlobalTools"
    }

    # List SARIF files
    Write-Section "SARIF files generated"
    $SarifFiles = Get-ChildItem -Path $SarifDir -Filter "*.json" -ErrorAction SilentlyContinue
    if ($SarifFiles) {
        $SarifFiles | ForEach-Object {
            Write-Information "  $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)"
        }
    }
    else {
        Write-Information "No SARIF files found"
    }

    # Activate NDepend license
    Write-Section "Activating NDepend license"
    if (-not [string]::IsNullOrWhiteSpace($NdependLicense)) {
        dotnet $NdependDll --RegLic $NdependLicense
        if ($LASTEXITCODE -ne 0) { Write-Warning "License activation returned exit code $LASTEXITCODE" }
    }
    else {
        Write-Warning "Skipping license activation (no license provided)"
    }

    # Run NDepend analysis
    Write-Section "Running NDepend analysis"
    $NdependArgs = @($NdependProject)
    if ($CoverageFiles) {
        $NdependArgs += "/CoverageFiles"
        foreach ($file in $CoverageFiles) {
            $NdependArgs += $file
        }
    }

    dotnet $NdependDll @NdependArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "NDepend analysis completed with exit code $LASTEXITCODE"
        $AnalysisFailed = $true
    }

}
catch {
    Write-Error "Script failed: $_"
    $AnalysisFailed = $true
}
finally {
    # Always deactivate license
    Write-Section "Deactivating NDepend license"
    if (-not [string]::IsNullOrWhiteSpace($NdependLicense)) {
        dotnet $NdependDll --UnregLic
        if ($LASTEXITCODE -ne 0) { Write-Warning "License deactivation returned exit code $LASTEXITCODE" }
    }
    else {
        Write-Information "Skipping license deactivation (no license provided)"
    }

    # Generate badges from NDepend trend data
    Write-Section "Generating quality badges"
    $NDBadgePath = Join-Path $ScriptDir "NDBadge.exe"
    # Find the most recent trend data file
    $TrendMetricsDir = Join-Path $ScriptDir "NDependOut\TrendMetrics"
    $TrendXml = if (Test-Path $TrendMetricsDir) {
        Get-ChildItem -Path $TrendMetricsDir -Filter "NDependTrendData*.xml" -File |
        Sort-Object Name -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    }
    else { $null }
    $BadgeDir = Join-Path $ScriptDir "badges"

    if ((Test-Path $NDBadgePath) -and $TrendXml -and (Test-Path $TrendXml)) {
        if (-not (Test-Path $BadgeDir)) {
            New-Item -ItemType Directory -Path $BadgeDir -Force | Out-Null
        }

        $Badges = @(
            @{ Metric = "# Lines of Code"; Output = "loc.svg" },
            @{ Metric = "# Source Files"; Output = "files.svg" },
            @{ Metric = "# Classes"; Output = "classes.svg" },
            @{ Metric = "# Methods"; Output = "methods.svg" },
            @{ Metric = "# Public Types"; Output = "public-api.svg" },
            @{ Metric = "Percentage of Comments"; Output = "comments.svg" },
            @{ Metric = "Average Cyclomatic Complexity for Methods"; Output = "complexity.svg" }
        )

        foreach ($badge in $Badges) {
            $outputPath = Join-Path $BadgeDir $badge.Output
            & $NDBadgePath --xml $TrendXml --metric $badge.Metric --output $outputPath 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Information "  Generated: $($badge.Output)"
            }
        }
        Write-Information "Badges saved to: $BadgeDir"
    }
    else {
        Write-Information "Skipping badge generation (NDBadge.exe or trend data not found)"
    }

    Write-Section "Done"
    
    if ($AnalysisFailed) {
        Write-Information "Note: Analysis completed with quality gate failures"
        exit 1
    }
}
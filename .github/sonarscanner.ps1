#Requires -Version 7.0
<#
.SYNOPSIS
    Static Analysis Scanner for QuanTAlib
.DESCRIPTION
    Runs SonarCloud and Qodana with code coverage on Windows
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Load tokens from User environment if not in process environment
if (-not $env:SONAR_TOKEN) {
    $env:SONAR_TOKEN = [System.Environment]::GetEnvironmentVariable("SONAR_TOKEN", "User")
}
if (-not $env:QODANA_TOKEN) {
    $env:QODANA_TOKEN = [System.Environment]::GetEnvironmentVariable("QODANA_TOKEN", "User")
}

# Verify tokens are available
if (-not $env:SONAR_TOKEN) {
    Write-Error "SONAR_TOKEN environment variable not set"
    exit 1
}
if (-not $env:QODANA_TOKEN) {
    Write-Error "QODANA_TOKEN environment variable not set"
    exit 1
}

# Coverage output directory for Qodana
$CoverageDir = ".qodana/code-coverage"
New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null

# Start SonarScanner analysis context
Write-Host "==> Starting SonarScanner analysis..." -ForegroundColor Cyan
dotnet-sonarscanner begin `
    /o:"mihakralj-quantalib" `
    /k:"mihakralj_QuanTAlib" `
    /d:sonar.token="$env:SONAR_TOKEN" `
    /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Build solution (within SonarScanner context)
Write-Host "==> Building solution..." -ForegroundColor Cyan
dotnet build --no-incremental
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Run tests with coverage (opencover format works for both SonarCloud and can be converted)
Write-Host "==> Running tests with coverage..." -ForegroundColor Cyan
dotnet test --no-build --collect:"XPlat Code Coverage" `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover,lcov
# Continue even if tests fail - we still want coverage data for analysis

# Copy coverage files to Qodana directory with unique names
Write-Host "==> Copying coverage files to Qodana directory..." -ForegroundColor Cyan
$coverageFiles = Get-ChildItem -Recurse -Filter "coverage.info" -ErrorAction SilentlyContinue
if ($coverageFiles) {
    $index = 0
    foreach ($file in $coverageFiles) {
        $destName = "coverage_$index.info"
        Copy-Item $file.FullName -Destination (Join-Path $CoverageDir $destName) -Force
        Write-Host "  Copied: $($file.FullName) -> $destName" -ForegroundColor Gray
        $index++
    }
} else {
    Write-Host "  WARNING: No coverage.info files found" -ForegroundColor Yellow
}

# End SonarScanner analysis
Write-Host "==> Completing SonarScanner analysis..." -ForegroundColor Cyan
dotnet-sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> SonarCloud: https://sonarcloud.io/project/overview?id=mihakralj_QuanTAlib" -ForegroundColor Green

# Run Qodana
Write-Host "==> Starting Qodana analysis..." -ForegroundColor Cyan

# Set CI environment to suppress interactive prompts
$env:CI = "true"
qodana scan --within-docker=false -l qodana-dotnet --coverage-dir $CoverageDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Qodana analysis complete" -ForegroundColor Green
Write-Host "==> Done!" -ForegroundColor Green

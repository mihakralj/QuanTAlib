#!/usr/bin/env pwsh
#requires -Version 7.0

<#
.SYNOPSIS
    Generates NDepend metric badges for QuanTAlib
.DESCRIPTION
    Creates SVG badges for key quality metrics that reinforce QuanTAlib's
    identity: scale, quality, low complexity, and comprehensive documentation.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$NDBadgePath = "$PSScriptRoot\NDBadge.exe",
    
    [Parameter(Mandatory = $false)]
    [string]$XmlPath = ".\ndepend\NDependOut\TrendMetrics\NDependTrendData2026.xml"
)

$ErrorActionPreference = 'Stop'

# Validate prerequisites
if (-not (Test-Path $NDBadgePath)) {
    Write-Error "NDBadge.exe not found at: $NDBadgePath"
    exit 1
}

if (-not (Test-Path $XmlPath)) {
    Write-Error "NDepend trend data XML not found at: $XmlPath"
    Write-Host "Please run ndepend.ps1 first to generate analysis data" -ForegroundColor Yellow
    exit 1
}

$ScriptDir = $PSScriptRoot
$OutputDir = Join-Path $ScriptDir "badges"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "=== Generating QuanTAlib Quality Badges ===" -ForegroundColor Cyan
Write-Host "Output directory: $OutputDir`n" -ForegroundColor Gray

# Selected badges for QuanTAlib README
Write-Host "Generating QuanTAlib badges..." -ForegroundColor Green

$HighValueBadges = @(
    @{
        Metric = "# Lines of Code"
        Output = "loc.svg"
        Description = "Total lines of code"
    },
    @{
        Metric = "# Source Files"
        Output = "files.svg"
        Description = "Source files"
    },
    @{
        Metric = "# Classes"
        Output = "classes.svg"
        Description = "Classes"
    },
    @{
        Metric = "# Methods"
        Output = "methods.svg"
        Description = "Methods"
    },
    @{
        Metric = "# Public Types"
        Output = "public-api.svg"
        Description = "Public API surface"
    },
    @{
        Metric = "Percentage of Comments"
        Output = "comments.svg"
        Description = "Comment percentage"
    },
    @{
        Metric = "Average Cyclomatic Complexity for Methods"
        Output = "complexity.svg"
        Description = "Average complexity"
    }
)

foreach ($badge in $HighValueBadges) {
    $outputPath = Join-Path $OutputDir $badge.Output
    Write-Host "  [$($badge.Output)]" -ForegroundColor Cyan -NoNewline
    Write-Host " $($badge.Description)" -ForegroundColor Gray
    
    & $NDBadgePath --xml $XmlPath --metric $badge.Metric --output $outputPath
    
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed to generate badge: $($badge.Output)"
    }
}


# Generate summary
Write-Host "`n=== Badge Generation Complete ===" -ForegroundColor Green
$generatedBadges = Get-ChildItem -Path $OutputDir -Filter "*.svg"
Write-Host "Generated $($generatedBadges.Count) badges in: $OutputDir"

Write-Host "`nGenerated badges:" -ForegroundColor Green
$HighValueBadges | ForEach-Object {
    Write-Host "  - $($_.Output.PadRight(20)) : $($_.Description)" -ForegroundColor Gray
}

Write-Host "`nMarkdown snippet for README.md:" -ForegroundColor Cyan
Write-Host @"

## Quality Metrics

[![LoC](ndepend/badges/loc.svg)]()
[![Files](ndepend/badges/files.svg)]()
[![Classes](ndepend/badges/classes.svg)]()
[![Methods](ndepend/badges/methods.svg)]()
[![Public API](ndepend/badges/public-api.svg)]()
[![Comments](ndepend/badges/comments.svg)]()
[![Complexity](ndepend/badges/complexity.svg)]()

"@ -ForegroundColor Gray

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
    Write-Information "Please run ndepend.ps1 first to generate analysis data"
    exit 1
}

$ScriptDir = $PSScriptRoot
$OutputDir = Join-Path $ScriptDir "badges"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Information "=== Generating QuanTAlib Quality Badges ==="
Write-Information "Output directory: $OutputDir`n"

# Selected badges for QuanTAlib README
Write-Information "Generating QuanTAlib badges..."

$HighValueBadges = @(
    @{
        Metric      = "# Lines of Code"
        Output      = "loc.svg"
        Description = "Total lines of code"
    },
    @{
        Metric      = "# Source Files"
        Output      = "files.svg"
        Description = "Source files"
    },
    @{
        Metric      = "# Classes"
        Output      = "classes.svg"
        Description = "Classes"
    },
    @{
        Metric      = "# Methods"
        Output      = "methods.svg"
        Description = "Methods"
    },
    @{
        Metric      = "# Public Types"
        Output      = "public-api.svg"
        Description = "Public API surface"
    },
    @{
        Metric      = "Percentage of Comments"
        Output      = "comments.svg"
        Description = "Comment percentage"
    },
    @{
        Metric      = "Average Cyclomatic Complexity for Methods"
        Output      = "complexity.svg"
        Description = "Average complexity"
    }
)

foreach ($badge in $HighValueBadges) {
    $outputPath = Join-Path $OutputDir $badge.Output
    Write-Information "  [$($badge.Output)] $($badge.Description)"
    
    & $NDBadgePath --xml $XmlPath --metric $badge.Metric --output $outputPath
    
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed to generate badge: $($badge.Output)"
    }
}


# Generate summary
Write-Information "`n=== Badge Generation Complete ==="
$generatedBadges = Get-ChildItem -Path $OutputDir -Filter "*.svg"
Write-Information "Generated $($generatedBadges.Count) badges in: $OutputDir"

Write-Information "`nGenerated badges:"
$HighValueBadges | ForEach-Object {
    Write-Information "  - $($_.Output.PadRight(20)) : $($_.Description)"
}

Write-Information "`nMarkdown snippet for README.md:"
Write-Information @"

## Quality Metrics

[![LoC](ndepend/badges/loc.svg)]()
[![Files](ndepend/badges/files.svg)]()
[![Classes](ndepend/badges/classes.svg)]()
[![Methods](ndepend/badges/methods.svg)]()
[![Public API](ndepend/badges/public-api.svg)]()
[![Comments](ndepend/badges/comments.svg)]()
[![Complexity](ndepend/badges/complexity.svg)]()

"@

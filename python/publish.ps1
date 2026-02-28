# publish.ps1 — Build NativeAOT shared library for the current platform.
# Usage: pwsh python/publish.ps1 [-Configuration Release] [-Runtime win-x64]
#
# The script publishes python.csproj as a NativeAOT shared library and
# copies the output to the quantalib package directory for _loader.py to find.

param(
    [string]$Configuration = "Release",
    [string]$Runtime = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Resolve paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $scriptDir "python.csproj"

if (-not (Test-Path $projectFile)) {
    Write-Error "Cannot find $projectFile"
    exit 1
}

# Auto-detect runtime if not specified
if (-not $Runtime) {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64") { "arm64" } else { "x64" }
        $Runtime = "win-$arch"
    }
    elseif ($IsMacOS) {
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64") { "arm64" } else { "x64" }
        $Runtime = "osx-$arch"
    }
    else {
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64") { "arm64" } else { "x64" }
        $Runtime = "linux-$arch"
    }
}

# Map runtime to Python platform tag directory
$platformDirMap = @{
    "win-x64"   = "win_amd64"
    "win-arm64" = "win_arm64"
    "linux-x64" = "linux_x86_64"
    "linux-arm64" = "linux_aarch64"
    "osx-x64"   = "macosx_x86_64"
    "osx-arm64" = "macosx_arm64"
}

$platformDir = $platformDirMap[$Runtime]
if (-not $platformDir) {
    Write-Error "Unknown runtime '$Runtime'. Supported: $($platformDirMap.Keys -join ', ')"
    exit 1
}

# Target output directory (direct to native/ subdirectory)
$nativeTargetDir = Join-Path $scriptDir "quantalib" "native" $platformDir

Write-Host "Publishing NativeAOT library..."
Write-Host "  Configuration : $Configuration"
Write-Host "  Runtime       : $Runtime"
Write-Host "  Project       : $projectFile"
Write-Host "  Output        : $nativeTargetDir"
Write-Host ""

# Publish directly to the target native directory
dotnet publish $projectFile `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $nativeTargetDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Verify native library exists
$libName = switch -Wildcard ($Runtime) {
    "win-*"   { "quantalib_native.dll" }
    "osx-*"   { "quantalib_native.dylib" }
    default   { "quantalib_native.so" }
}

$nativeLib = Join-Path $nativeTargetDir $libName
if (-not (Test-Path $nativeLib)) {
    Write-Error "Native library not found: $nativeLib"
    exit 1
}

$size = [math]::Round((Get-Item $nativeLib).Length / 1MB, 2)

Write-Host ""
Write-Host "SUCCESS: $nativeLib ($size MB)"
Write-Host ""
Write-Host "To run tests:"
Write-Host "  cd $scriptDir"
Write-Host "  python -m pytest tests/ -v"

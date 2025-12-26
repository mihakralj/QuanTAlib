#!/bin/bash
set -e

echo "Using pre-installed .NET SDKs..."
dotnet --list-sdks

echo "Modifying csproj files to target only net8.0 for Qodana..."
# Replace multi-targeting with single target net8.0
find . -name "*.csproj" -exec sed -i 's|<TargetFrameworks>net10.0;net8.0</TargetFrameworks>|<TargetFramework>net8.0</TargetFramework>|g' {} +
# Just in case some files still have single target net10.0
find . -name "*.csproj" -exec sed -i 's|<TargetFramework>net10.0</TargetFramework>|<TargetFramework>net8.0</TargetFramework>|g' {} +

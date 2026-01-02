#!/bin/bash
set -e

# Source zshrc to get NDEPEND_LICENSE if not already set
if [ -z "$NDEPEND_LICENSE" ]; then
    source ~/.zshrc 2>/dev/null || true
fi

NDEPEND_DLL=~/NDepend/net10.0/NDepend.Console.MultiOS.dll
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."
COVERAGE_DIR="$SCRIPT_DIR/coverage"
SARIF_DIR="$PROJECT_ROOT/.sarif"

echo "=== Creating .sarif directory for Roslyn analyzer output ==="
mkdir -p "$SARIF_DIR"

echo "=== Cleaning and building solution (generates SARIF files) ==="
dotnet restore "$PROJECT_ROOT/QuanTAlib.sln" -v q
dotnet clean "$PROJECT_ROOT/QuanTAlib.sln" -v q
rm -rf "$COVERAGE_DIR"
dotnet build "$PROJECT_ROOT/QuanTAlib.sln" -c Debug --no-incremental

echo "=== Running tests with coverage ==="
dotnet test "$PROJECT_ROOT/lib/QuanTAlib.Tests.csproj" \
    -c Debug \
    --no-build \
    --collect:"XPlat Code Coverage" \
    --settings "$PROJECT_ROOT/coverlet.runsettings" \
    --results-directory:"$COVERAGE_DIR"

# Find the generated coverage file (opencover format)
COVERAGE_FILE=$(find "$COVERAGE_DIR" -name "coverage.opencover.xml" -type f | head -1)
echo "Coverage file: $COVERAGE_FILE"

# List generated SARIF files
echo "=== SARIF files generated ==="
ls -la "$SARIF_DIR"/*.json 2>/dev/null || echo "No SARIF files found"

echo "=== Activating NDepend license ==="
dotnet "$NDEPEND_DLL" --RegLic "$NDEPEND_LICENSE"

echo "=== Running NDepend analysis ==="
# NDepend automatically picks up SARIF files from $(SolutionDir)/.sarif when project references .sln
dotnet "$NDEPEND_DLL" "$SCRIPT_DIR/quantalib.ndproj" /CoverageFiles "$COVERAGE_FILE" || ANALYSIS_FAILED=1

echo "=== Deactivating NDepend license ==="
dotnet "$NDEPEND_DLL" --UnregLic

echo "=== Done ==="

if [ "${ANALYSIS_FAILED:-0}" -eq 1 ]; then
    echo "Note: Analysis completed with quality gate failures"
    exit 1
fi

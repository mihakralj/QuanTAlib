#!/bin/bash
#-------------------------------------------------------------------------------
# Qodana Local Analysis Script
# Runs JetBrains Qodana for .NET analysis locally using Docker
# Generates OpenCover coverage data and feeds it to Qodana
#-------------------------------------------------------------------------------

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="$SCRIPT_DIR/results"
COVERAGE_DIR="$SCRIPT_DIR/code-coverage"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║           Qodana for .NET - Local Analysis                   ║${NC}"
echo -e "${GREEN}║           Image: jetbrains/qodana-dotnet:2025.3              ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════════╝${NC}"

# Check for Docker
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed or not in PATH${NC}"
    exit 1
fi

# Clean up old results and create fresh directories with proper permissions
echo -e "${GREEN}=== Cleaning up old results ===${NC}"
rm -rf "$RESULTS_DIR"
rm -rf "$COVERAGE_DIR"
mkdir -p "$RESULTS_DIR/log"
mkdir -p "$RESULTS_DIR/report"
mkdir -p "$COVERAGE_DIR"
chmod -R 777 "$RESULTS_DIR"
chmod -R 777 "$COVERAGE_DIR"

# Check for QODANA_TOKEN
if [ -z "$QODANA_TOKEN" ]; then
    echo -e "${YELLOW}Warning: QODANA_TOKEN not set. Running in trial mode (limited features).${NC}"
    echo -e "${YELLOW}Set QODANA_TOKEN environment variable for full functionality.${NC}"
    echo ""
fi

echo -e "${GREEN}Project directory:${NC} $PROJECT_DIR"
echo -e "${GREEN}Results directory:${NC} $RESULTS_DIR"
echo -e "${GREEN}Coverage directory:${NC} $COVERAGE_DIR"
echo ""

# Step 1: Build the solution
echo -e "${GREEN}=== Building solution ===${NC}"
dotnet restore "$PROJECT_DIR/QuanTAlib.sln" -v q
dotnet build "$PROJECT_DIR/QuanTAlib.sln" -c Debug --no-incremental

# Step 2: Run tests with OpenCover coverage
echo -e "${GREEN}=== Running tests with OpenCover coverage ===${NC}"
rm -rf "$COVERAGE_DIR"/*
dotnet test "$PROJECT_DIR/lib/QuanTAlib.Tests.csproj" \
    -c Debug \
    --no-build \
    --collect:"XPlat Code Coverage" \
    --settings "$PROJECT_DIR/coverlet.runsettings" \
    --results-directory:"$COVERAGE_DIR"

# Find the generated coverage file
COVERAGE_FILE=$(find "$COVERAGE_DIR" -name "coverage.opencover.xml" -type f | head -1)
if [ -n "$COVERAGE_FILE" ]; then
    echo -e "${GREEN}Coverage file found:${NC} $COVERAGE_FILE"
    # Copy to a known location for Qodana
    cp "$COVERAGE_FILE" "$COVERAGE_DIR/coverage.xml"
else
    echo -e "${YELLOW}Warning: No coverage file found${NC}"
fi

# Step 3: Run Qodana analysis
echo -e "${GREEN}=== Starting Qodana analysis ===${NC}"
echo ""

# Run as current user to avoid permission issues on macOS
docker run --rm \
    --user "$(id -u):$(id -g)" \
    -v "$PROJECT_DIR":/data/project \
    -v "$RESULTS_DIR":/data/results \
    -v "$COVERAGE_DIR":/data/coverage \
    ${QODANA_TOKEN:+-e QODANA_TOKEN="$QODANA_TOKEN"} \
    jetbrains/qodana-dotnet:2025.3 \
    --config /data/project/.qodana/qodana.yaml \
    --property=qodana.net.targetFrameworks=net10.0 \
    --property=qodana.net.configuration=Debug \
    "$@"

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║                    Analysis Complete                         ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${GREEN}Results saved to:${NC} $RESULTS_DIR"
echo -e "${GREEN}Open report:${NC} open $RESULTS_DIR/report/index.html"

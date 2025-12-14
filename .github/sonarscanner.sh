#!/bin/bash
set -euo pipefail

# Static Analysis Scanner for QuanTAlib
# Runs SonarCloud and Qodana with code coverage

# Tokens must be set as environment variables
if [[ -z "${SONAR_TOKEN:-}" ]]; then
    echo "Error: SONAR_TOKEN environment variable not set"
    exit 1
fi
if [[ -z "${QODANA_TOKEN:-}" ]]; then
    echo "Error: QODANA_TOKEN environment variable not set"
    exit 1
fi

# Coverage output directory for Qodana
COVERAGE_DIR=".qodana/code-coverage"

# Run SonarCloud
echo "==> Starting SonarScanner analysis..."

dotnet sonarscanner begin \
    /o:"mihakralj-quantalib" \
    /k:"mihakralj_QuanTAlib" \
    /d:sonar.token="$SONAR_TOKEN" \
    /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"

echo "==> Building solution..."
dotnet build --no-incremental
if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

echo "==> Running tests with coverage..."
mkdir -p "$COVERAGE_DIR"
dotnet test --no-build --collect:"XPlat Code Coverage" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=lcov,opencover
if [ $? -ne 0 ]; then
    echo "Error: Tests failed"
    exit 1
fi

# Copy coverage to Qodana directory
find . -name "coverage.info" -exec cp {} "$COVERAGE_DIR/" \;

dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"

echo "==> SonarCloud: https://sonarcloud.io/project/overview?id=mihakralj_QuanTAlib"

# Run Qodana
echo "==> Starting Qodana analysis..."

QODANA_TOKEN="$QODANA_TOKEN" qodana scan \
    --coverage-dir "$COVERAGE_DIR"

echo "==> Qodana analysis complete"

echo "==> Done!"

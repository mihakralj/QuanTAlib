#!/bin/bash
set -euo pipefail

# Codacy Coverage Upload for QuanTAlib
# Requires CODACY_PROJECT_TOKEN environment variable

if [[ -z "${CODACY_PROJECT_TOKEN:-}" ]]; then
    echo "Error: CODACY_PROJECT_TOKEN environment variable not set"
    exit 1
fi

echo "==> Building solution..."
dotnet build --no-incremental

echo "==> Running tests with coverage..."
dotnet test --no-build --collect:"XPlat Code Coverage" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Find the coverage file
COVERAGE_FILE=$(find . -name "coverage.opencover.xml" -type f | head -1)

if [[ -z "$COVERAGE_FILE" ]]; then
    echo "Error: No coverage file found"
    exit 1
fi

echo "==> Uploading coverage to Codacy..."
echo "    Coverage file: $COVERAGE_FILE"

bash <(curl -Ls https://coverage.codacy.com/get.sh) report \
    -r "$COVERAGE_FILE" \
    --project-token "$CODACY_PROJECT_TOKEN"

echo "==> Done! View results at https://app.codacy.com/gh/mihakralj/QuanTAlib"

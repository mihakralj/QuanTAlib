#!/bin/bash
# Static Analysis Scanner for QuanTAlib
# Runs SonarCloud, Codacy coverage, and Qodana
# 
# Usage: wsl -d Debian -- bash /mnt/z/github/QuanTAlib/.github/scanner.sh
# Or from WSL: cd /mnt/z/github/QuanTAlib && ./.github/scanner.sh

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
GRAY='\033[0;90m'
NC='\033[0m'

log_info() { echo -e "${CYAN}==> $1${NC}"; }
log_success() { echo -e "${GREEN}==> $1${NC}"; }
log_warn() { echo -e "${YELLOW}WARNING: $1${NC}"; }
log_error() { echo -e "${RED}ERROR: $1${NC}"; }
log_detail() { echo -e "${GRAY}    $1${NC}"; }

# Change to project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

# Set DOTNET_ROOT for tools like dotnet-sonarscanner
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"

log_info "Working directory: $(pwd)"

# Parse arguments
SKIP_SONAR=false
SKIP_CODACY=false
SKIP_QODANA=false
SKIP_BUILD=false

for arg in "$@"; do
    case $arg in
        --skip-sonar) SKIP_SONAR=true ;;
        --skip-codacy) SKIP_CODACY=true ;;
        --skip-qodana) SKIP_QODANA=true ;;
        --skip-build) SKIP_BUILD=true ;;
        --codacy-only) SKIP_SONAR=true; SKIP_QODANA=true; SKIP_BUILD=true ;;
        --help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --skip-sonar    Skip SonarCloud analysis"
            echo "  --skip-codacy   Skip Codacy coverage upload"
            echo "  --skip-qodana   Skip Qodana analysis"
            echo "  --skip-build    Skip build/test (use existing coverage)"
            echo "  --codacy-only   Only upload coverage to Codacy"
            exit 0
            ;;
    esac
done

# Load tokens from environment or Windows user environment
load_token() {
    local var_name=$1
    local current_value="${!var_name}"
    
    if [ -z "$current_value" ]; then
        # Try to get from Windows environment via PowerShell
        if command -v powershell.exe &> /dev/null; then
            current_value=$(powershell.exe -NoProfile -Command "[System.Environment]::GetEnvironmentVariable('$var_name', 'User')" 2>/dev/null | tr -d '\r')
        fi
    fi
    
    if [ -n "$current_value" ]; then
        export "$var_name"="$current_value"
        return 0
    fi
    return 1
}

# Load required tokens
load_token "SONAR_TOKEN" || true
load_token "QODANA_TOKEN" || true
load_token "CODACY_PROJECT_TOKEN" || true

# Verify tokens
if [ -z "$SONAR_TOKEN" ] && [ "$SKIP_SONAR" = false ]; then
    log_warn "SONAR_TOKEN not set - SonarCloud analysis will be skipped"
    SKIP_SONAR=true
fi

if [ -z "$CODACY_PROJECT_TOKEN" ] && [ "$SKIP_CODACY" = false ]; then
    log_warn "CODACY_PROJECT_TOKEN not set - Codacy upload will be skipped"
    SKIP_CODACY=true
fi

if [ -z "$QODANA_TOKEN" ] && [ "$SKIP_QODANA" = false ]; then
    log_warn "QODANA_TOKEN not set - Qodana analysis will be skipped"
    SKIP_QODANA=true
fi

# Coverage directory for Qodana
COVERAGE_DIR=".qodana/code-coverage"
mkdir -p "$COVERAGE_DIR"

# ============================================
# Build and Test with Coverage
# ============================================
if [ "$SKIP_BUILD" = false ]; then
    # Clean any leftover SonarQube state from previous runs
    rm -rf /tmp/.sonarqube .sonarqube 2>/dev/null || true
    
    # Clean obj/bin directories to avoid cross-platform cache issues
    log_info "Cleaning build artifacts..."
    find . -type d -name "obj" -exec rm -rf {} + 2>/dev/null || true
    find . -type d -name "bin" -exec rm -rf {} + 2>/dev/null || true
    
    log_info "Building solution..."
    # Skip GitVersion in WSL builds - it has path issues with Windows mounts
    dotnet build /p:DisableGitVersionTask=true /p:Version=0.0.0-wsl /p:AssemblyVersion=0.0.0.0 /p:FileVersion=0.0.0.0
    
    log_info "Running tests with coverage..."
    dotnet test --no-build --collect:"XPlat Code Coverage" \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover,lcov || true
    
    # Copy coverage files to Qodana directory and convert Windows paths to Linux
    log_info "Copying coverage files for Qodana..."
    index=0
    find . -name "coverage.info" -type f | while read -r file; do
        # Convert Windows paths (Z:\github\...) to Linux paths (/mnt/z/github/...)
        sed -e 's|SF:Z:\\|SF:/mnt/z/|g' \
            -e 's|SF:z:\\|SF:/mnt/z/|g' \
            -e 's|\\|/|g' \
            "$file" > "$COVERAGE_DIR/coverage_$index.info"
        log_detail "Converted: $file -> coverage_$index.info (Windows→Linux paths)"
        ((index++)) || true
    done
fi

# ============================================
# SonarCloud Analysis (requires dotnet-sonarscanner)
# ============================================
if [ "$SKIP_SONAR" = false ]; then
    if command -v dotnet-sonarscanner &> /dev/null; then
        log_info "Starting SonarCloud analysis..."
        
        # Clean sonarqube temp from previous runs
        rm -rf /tmp/.sonarqube .sonarqube 2>/dev/null || true
        
        # Remove generated AssemblyInfo files that may conflict with fresh build
        find . -name "*.AssemblyInfo.cs" -path "*/obj/*" -delete 2>/dev/null || true
        find . -name "*.AssemblyInfoInputs.cache" -path "*/obj/*" -delete 2>/dev/null || true
        
        dotnet-sonarscanner begin \
            /o:"mihakralj-quantalib" \
            /k:"mihakralj_QuanTAlib" \
            /d:sonar.token="$SONAR_TOKEN" \
            /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" \
            /d:sonar.exclusions=".github/**"
        
        # Full rebuild with SonarScanner targets (skip GitVersion in WSL)
        dotnet build --no-incremental /p:DisableGitVersionTask=true /p:Version=0.0.0-wsl /p:AssemblyVersion=0.0.0.0 /p:FileVersion=0.0.0.0
        
        dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"
        
        log_success "SonarCloud: https://sonarcloud.io/project/overview?id=mihakralj_QuanTAlib"
    else
        log_warn "dotnet-sonarscanner not found - skipping SonarCloud"
    fi
fi

# ============================================
# Codacy Coverage Upload
# ============================================
if [ "$SKIP_CODACY" = false ]; then
    log_info "Uploading coverage to Codacy..."
    
    # Find the most recent coverage files (one per test project)
    coverage_files=$(find . -name "coverage.opencover.xml" -type f -printf '%T@ %p\n' | sort -rn | head -2 | cut -d' ' -f2-)
    
    if [ -n "$coverage_files" ]; then
        for file in $coverage_files; do
            log_detail "Uploading: $file"
            bash <(curl -Ls https://coverage.codacy.com/get.sh) report -r "$file" --partial || true
        done
        
        # Send final notification
        log_detail "Finalizing coverage report..."
        bash <(curl -Ls https://coverage.codacy.com/get.sh) final || true
        
        log_success "Codacy: https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard"
    else
        log_warn "No coverage.opencover.xml files found"
    fi
fi

# ============================================
# Qodana Analysis
# ============================================
if [ "$SKIP_QODANA" = false ]; then
    if command -v qodana &> /dev/null; then
        log_info "Starting Qodana analysis..."
        
        export CI=true
        qodana scan --within-docker=false -l qodana-dotnet --coverage-dir "$COVERAGE_DIR" || true
        
        log_success "Qodana analysis complete"
    else
        log_warn "qodana not found - skipping Qodana analysis"
    fi
fi

log_success "Done!"

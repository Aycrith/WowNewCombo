#!/bin/bash

# WowClassicGrindBot Autonomous Test Runner
# Executes comprehensive test suite with optional WoW client integration

set -e  # Exit on first error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse command-line arguments
WITH_WOW=false
FULL=false
SKIP_BUILD=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --with-wow)
            WITH_WOW=true
            shift
            ;;
        --full)
            FULL=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--with-wow] [--full] [--skip-build]"
            exit 1
            ;;
    esac
done

# Setup
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP=$(date +%Y-%m-%d-%H-%M-%S)
TEST_OUTPUT_DIR="$PROJECT_ROOT/test-output/$TIMESTAMP"
mkdir -p "$TEST_OUTPUT_DIR"

# Summary tracking
PASSED=0
FAILED=0
SKIPPED=0

# Logging function
log_phase() {
    local phase=$1
    local status=$2
    echo -e "${BLUE}[$phase]${NC} $status"
}

# Phase success
phase_success() {
    local phase=$1
    echo -e "${GREEN}✓${NC} $phase passed"
    ((PASSED++))
}

# Phase failure
phase_failure() {
    local phase=$1
    local logfile=$2
    echo -e "${RED}✗${NC} $phase failed (see $logfile)"
    ((FAILED++))
}

# Phase skipped
phase_skipped() {
    local phase=$1
    echo -e "${YELLOW}⊘${NC} $phase skipped"
    ((SKIPPED++))
}

echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}  WowClassicGrindBot Test Runner${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""
echo "Output directory: $TEST_OUTPUT_DIR"
echo "Timestamp: $TIMESTAMP"
echo ""

# ========================================
# PHASE 1: PRE-FLIGHT CHECKS
# ========================================
log_phase "PRE-FLIGHT" "Starting pre-flight checks..."

# Check .NET SDK version
if ! command -v dotnet &> /dev/null; then
    phase_failure "PRE-FLIGHT: .NET SDK not found"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
log_phase "PRE-FLIGHT" ".NET SDK: $DOTNET_VERSION"

# Check global.json
if [ ! -f "$PROJECT_ROOT/global.json" ]; then
    phase_failure "PRE-FLIGHT" "global.json not found"
    exit 1
fi

log_phase "PRE-FLIGHT" "global.json found"
phase_success "PRE-FLIGHT"
echo ""

# ========================================
# PHASE 2: BUILD
# ========================================
if [ "$SKIP_BUILD" = true ]; then
    phase_skipped "BUILD (--skip-build)"
    echo ""
else
    log_phase "BUILD" "Running dotnet build..."
    BUILD_LOG="$TEST_OUTPUT_DIR/build.log"

    if cd "$PROJECT_ROOT" && dotnet build MasterOfPuppets.sln -c Debug > "$BUILD_LOG" 2>&1; then
        phase_success "BUILD"
    else
        phase_failure "BUILD" "$BUILD_LOG"
        echo ""
        echo "Build output:"
        tail -50 "$BUILD_LOG"
        exit 1
    fi
    echo ""
fi

# ========================================
# PHASE 3: UNIT TESTS
# ========================================
log_phase "UNIT-TESTS" "Running CoreUnitTests..."
UNIT_LOG="$TEST_OUTPUT_DIR/unit-tests.log"
UNIT_TRX="$TEST_OUTPUT_DIR/unit-results.trx"

if cd "$PROJECT_ROOT" && dotnet test CoreUnitTests \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=$UNIT_TRX" \
    > "$UNIT_LOG" 2>&1; then
    phase_success "UNIT-TESTS"
else
    phase_failure "UNIT-TESTS" "$UNIT_LOG"
    echo ""
    echo "Test output (last 30 lines):"
    tail -30 "$UNIT_LOG"
fi
echo ""

# ========================================
# PHASE 4: FRONTEND TESTS
# ========================================
log_phase "FRONTEND-TESTS" "Running FrontendUnitTests..."
FRONTEND_LOG="$TEST_OUTPUT_DIR/frontend-tests.log"

if cd "$PROJECT_ROOT" && dotnet test FrontendUnitTests \
    --logger "console;verbosity=normal" \
    > "$FRONTEND_LOG" 2>&1; then
    phase_success "FRONTEND-TESTS"
else
    # Frontend tests may not exist; graceful skip
    if grep -q "not found" "$FRONTEND_LOG" 2>/dev/null; then
        phase_skipped "FRONTEND-TESTS (project not found)"
    else
        phase_failure "FRONTEND-TESTS" "$FRONTEND_LOG"
    fi
fi
echo ""

# ========================================
# PHASE 5: INTEGRATION TESTS
# ========================================
log_phase "INTEGRATION-TESTS" "Running CoreManualTests (MockWoW scenarios)..."
INTEGRATION_LOG="$TEST_OUTPUT_DIR/integration-tests.log"

if cd "$PROJECT_ROOT" && dotnet test CoreManualTests \
    --filter "MockWoW" \
    --logger "console;verbosity=normal" \
    > "$INTEGRATION_LOG" 2>&1; then
    phase_success "INTEGRATION-TESTS"
else
    # Integration tests may not exist or may be optional
    if grep -q "No tests matched" "$INTEGRATION_LOG" 2>/dev/null || \
       grep -q "not found" "$INTEGRATION_LOG" 2>/dev/null; then
        phase_skipped "INTEGRATION-TESTS (no MockWoW scenarios found)"
    else
        phase_failure "INTEGRATION-TESTS" "$INTEGRATION_LOG"
    fi
fi
echo ""

# ========================================
# PHASE 6: OPTIONAL FULL INTEGRATION (--with-wow)
# ========================================
if [ "$WITH_WOW" = true ]; then
    log_phase "E2E-INTEGRATION" "Looking for running WoW client..."

    # Check if WoW.exe is running
    if pgrep -x "WoW.exe" > /dev/null 2>&1 || \
       pgrep -x "Wow.exe" > /dev/null 2>&1; then
        log_phase "E2E-INTEGRATION" "WoW.exe detected, starting BlazorServer..."

        E2E_LOG="$TEST_OUTPUT_DIR/e2e-integration.log"

        # Start BlazorServer in background
        cd "$PROJECT_ROOT"
        dotnet run --project BlazorServer -c Debug > "$E2E_LOG" 2>&1 &
        SERVER_PID=$!

        # Wait for server readiness (max 30 seconds)
        log_phase "E2E-INTEGRATION" "Waiting for server readiness..."
        for i in {1..30}; do
            if curl -s http://localhost:5000/health > /dev/null 2>&1; then
                log_phase "E2E-INTEGRATION" "Server is ready"
                break
            fi
            sleep 1
        done

        # Verify addon data
        if curl -s http://localhost:5000/api/diagnostics/frame-health > /dev/null 2>&1; then
            phase_success "E2E-INTEGRATION"
        else
            phase_failure "E2E-INTEGRATION" "$E2E_LOG"
        fi

        # Kill server
        kill $SERVER_PID 2>/dev/null || true
    else
        phase_skipped "E2E-INTEGRATION (WoW.exe not running)"
    fi
    echo ""
fi

# ========================================
# PHASE 7: OPTIONAL BENCHMARKS (--full)
# ========================================
if [ "$FULL" = true ]; then
    log_phase "BENCHMARKS" "Running BenchmarkDotNet suite..."
    BENCH_LOG="$TEST_OUTPUT_DIR/benchmarks.log"

    if cd "$PROJECT_ROOT" && dotnet run --project Benchmarks -c Release \
        > "$BENCH_LOG" 2>&1; then
        phase_success "BENCHMARKS"
    else
        phase_failure "BENCHMARKS" "$BENCH_LOG"
    fi
    echo ""
fi

# ========================================
# SUMMARY
# ========================================
echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}  Test Summary${NC}"
echo -e "${BLUE}======================================${NC}"
echo -e "${GREEN}Passed:${NC}  $PASSED"
echo -e "${RED}Failed:${NC}  $FAILED"
echo -e "${YELLOW}Skipped:${NC} $SKIPPED"
echo ""
echo "Results directory: $TEST_OUTPUT_DIR"
echo ""

# Exit with appropriate code
if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✓ All test phases completed successfully${NC}"
    exit 0
else
    echo -e "${RED}✗ Some test phases failed${NC}"
    exit 1
fi

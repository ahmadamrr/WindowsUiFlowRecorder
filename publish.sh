#!/usr/bin/env bash
set -euo pipefail

# Windows UI Flow Recorder - Build & Publish Script
# Produces a portable, self-contained build at ./publish/

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/src/WindowsUiFlowRecorder.Presentation"
PUBLISH_DIR="$SCRIPT_DIR/publish"
CONFIGURATION="${1:-Release}"
RID="${2:-win-x64}"

echo "=== Windows UI Flow Recorder Build & Publish ==="
echo "Configuration: $CONFIGURATION"
echo "Runtime:       $RID"
echo "Target:        $PUBLISH_DIR"
echo ""

# Step 1: Restore
echo "[1/4] Restoring packages..."
dotnet restore "$PROJECT_DIR/WindowsUiFlowRecorder.Presentation.csproj"

# Step 2: Build
echo "[2/4] Building solution..."
dotnet build "$SCRIPT_DIR/WindowsUiFlowRecorder.sln" -c "$CONFIGURATION" --no-restore

# Step 3: Run architecture compliance tests
echo "[3/4] Running architecture compliance tests..."
dotnet test "$SCRIPT_DIR/tests/WindowsUiFlowRecorder.Application.Tests/WindowsUiFlowRecorder.Application.Tests.csproj" \
    -c "$CONFIGURATION" --no-restore --filter "FullyQualifiedName~ArchitectureComplianceTests"

# Step 4: Publish
echo "[4/4] Publishing portable build..."
rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT_DIR/WindowsUiFlowRecorder.Presentation.csproj" \
    -c "$CONFIGURATION" \
    -r "$RID" \
    --self-contained true \
    -o "$PUBLISH_DIR" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

echo ""
echo "=== Build complete ==="
echo "Output: $PUBLISH_DIR"
echo ""
echo "Contents:"
ls -lh "$PUBLISH_DIR" | head -20
echo ""
echo "To run: $PUBLISH_DIR/WindowsUiFlowRecorder.Presentation.exe"
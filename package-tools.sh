#!/bin/bash
# Build and package SchemaSmithyFree CLI tools for all supported RIDs
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_DIR="$SCRIPT_DIR/Release"
TOOLS_DIR="$RELEASE_DIR/Tools"
PACKAGES_DIR="$RELEASE_DIR/Packages"
RIDS=("win-x64" "win-arm64" "linux-x64" "linux-arm64" "osx-x64" "osx-arm64")
TOOLS=("SchemaQuench" "SchemaTongs" "DataTongs")

rm -rf "$TOOLS_DIR" "$PACKAGES_DIR"
mkdir -p "$PACKAGES_DIR"

for rid in "${RIDS[@]}"; do
  RID_DIR="$TOOLS_DIR/$rid"
  mkdir -p "$RID_DIR"
  echo "Building for $rid..."

  for tool in "${TOOLS[@]}"; do
    echo "  Publishing $tool..."
    dotnet publish "$SCRIPT_DIR/$tool/$tool.csproj" -c Release -r "$rid" --self-contained -o "$RID_DIR"
  done

  ZIP_NAME="SchemaSmithyMSSQL-Community-$rid.zip"
  echo "  Creating $ZIP_NAME..."
  (cd "$RID_DIR" && zip -r "$PACKAGES_DIR/$ZIP_NAME" .)
done

echo "Done. Packages in $PACKAGES_DIR:"
ls -la "$PACKAGES_DIR"

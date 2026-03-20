#!/bin/bash
# Publish SchemaSmithyFree CLI tools for local development
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/Release/Tools"
RID="${1:-}"

# Auto-detect RID if not provided
if [ -z "$RID" ]; then
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64)  RID="linux-x64" ;;
    Linux-aarch64) RID="linux-arm64" ;;
    Darwin-x86_64) RID="osx-x64" ;;
    Darwin-arm64)  RID="osx-arm64" ;;
    MINGW*|MSYS*)  RID="win-x64" ;;
    *)             echo "Cannot detect RID. Usage: $0 [rid]"; exit 1 ;;
  esac
fi

echo "Publishing tools to $OUTPUT_DIR (RID: $RID)..."
mkdir -p "$OUTPUT_DIR"

for tool in SchemaQuench SchemaTongs DataTongs; do
  echo "  Publishing $tool..."
  dotnet publish "$SCRIPT_DIR/$tool/$tool.csproj" -c Release -r "$RID" --self-contained -o "$OUTPUT_DIR"
done

echo "Done. Tools published to $OUTPUT_DIR"

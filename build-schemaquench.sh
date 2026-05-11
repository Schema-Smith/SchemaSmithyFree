#!/usr/bin/env bash
set -e

echo "Building SchemaQuench for Docker demos..."

# Detect architecture
ARCH=$(uname -m)
case "$ARCH" in
    x86_64)  RID="linux-x64" ;;
    aarch64|arm64) RID="linux-arm64" ;;
    *) echo "Unsupported architecture: $ARCH"; exit 1 ;;
esac

echo "  Architecture: $RID"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
dotnet publish "$SCRIPT_DIR/SchemaQuench/SchemaQuench.csproj" -c Release -r "$RID" --self-contained -o "$SCRIPT_DIR/SchemaQuench/publish"

if [ ! -f "$SCRIPT_DIR/SchemaQuench/publish/SchemaQuench" ]; then
    echo "BUILD FAILED: SchemaQuench/publish/SchemaQuench was not produced. Check the dotnet publish output above."
    echo "Likely cause: .NET 10 SDK is not installed, or a NuGet restore step failed silently."
    exit 1
fi

echo "  Build complete: SchemaQuench/publish/"

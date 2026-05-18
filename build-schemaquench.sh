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

# Read $(IcuVersion) from Directory.Build.props so the verification below stays in
# lockstep with the package version. Single source of truth.
ICU_VERSION=$(sed -n 's:.*<IcuVersion>\(.*\)</IcuVersion>.*:\1:p' "$SCRIPT_DIR/Directory.Build.props")
if [ -z "$ICU_VERSION" ]; then
    echo "BUILD FAILED: Could not read <IcuVersion> from Directory.Build.props."
    exit 1
fi

# Clean publish/ so NuGet native runtime assets (libicu*.so) are always re-staged.
# Incremental dotnet publish skips re-copying package native assets when MSBuild
# considers the project up-to-date, which can leave publish/ missing the ICU
# libraries and crash the Linux container at the first globalization call with
# "Failed to load app-local ICU: libicudata.so.$IcuVersion".
rm -rf "$SCRIPT_DIR/SchemaQuench/publish"

dotnet publish "$SCRIPT_DIR/SchemaQuench/SchemaQuench.csproj" -c Release -r "$RID" --self-contained -o "$SCRIPT_DIR/SchemaQuench/publish"

if [ ! -f "$SCRIPT_DIR/SchemaQuench/publish/SchemaQuench" ]; then
    echo "BUILD FAILED: SchemaQuench/publish/SchemaQuench was not produced. Check the dotnet publish output above."
    echo "Likely cause: .NET 10 SDK is not installed, or a NuGet restore step failed silently."
    exit 1
fi

for icu in libicudata libicui18n libicuuc; do
    if [ ! -f "$SCRIPT_DIR/SchemaQuench/publish/${icu}.so.${ICU_VERSION}" ]; then
        echo "BUILD FAILED: ${icu}.so.${ICU_VERSION} missing from publish/ — Microsoft.ICU.ICU4C.Runtime native assets were not staged."
        echo "Likely cause: NuGet restore did not pull the linux runtime native assets for this RID."
        exit 1
    fi
done

echo "  Build complete: SchemaQuench/publish/"

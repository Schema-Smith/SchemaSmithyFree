#!/usr/bin/env bash
# Render packaging/aur/PKGBUILD for a published release: substitute the version and the two linux
# tarball SHA-256s (read from the release's authoritative SHA256SUMS asset). Writes the rendered
# PKGBUILD to $1 (default: ./PKGBUILD.rendered).
set -euo pipefail
VERSION="${SS_VERSION:?SS_VERSION required}"
REPO="Schema-Smith/SchemaSmith"
OUT="${1:-PKGBUILD.rendered}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

tmp="$(mktemp -d)"
gh release download "v${VERSION}" --repo "$REPO" --pattern SHA256SUMS --dir "$tmp"

sha_of() { # $1 = asset filename -> its sha256 (matches SHA256SUMS' basename line)
  awk -v n="$1" '{ f=$NF; sub(/^\*/,"",f); if (f==n) { print $1; found=1; exit } } END { if(!found) exit 1 }' \
    "$tmp/SHA256SUMS"
}

sha_x64="$(sha_of "SchemaSmith-${VERSION}-linux-x64.tar.gz")" || { echo "::error::x64 sha not found"; exit 1; }
sha_arm64="$(sha_of "SchemaSmith-${VERSION}-linux-arm64.tar.gz")" || { echo "::error::arm64 sha not found"; exit 1; }

sed -e "s/__VERSION__/${VERSION}/g" \
    -e "s/__SHA_X64__/${sha_x64}/g" \
    -e "s/__SHA_ARM64__/${sha_arm64}/g" \
    "$HERE/PKGBUILD" > "$OUT"
echo "Rendered PKGBUILD -> $OUT (x64=$sha_x64 arm64=$sha_arm64)"

#!/usr/bin/env bash
# Zero-dependency unit tests for the SchemaSmith action resolution functions.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
source "$DIR/../schemasmith-deploy.sh" --lib-only

fails=0
assert_eq() { # $1=actual $2=expected $3=label
  if [ "$1" = "$2" ]; then echo "ok   - $3"; else echo "FAIL - $3: got '$1' want '$2'"; fails=$((fails+1)); fi
}
assert_fail() { # $1=label ; reads command via "$@" from index 2
  local label="$1"; shift
  if "$@" >/dev/null 2>&1; then echo "FAIL - $label: expected non-zero"; fails=$((fails+1)); else echo "ok   - $label"; fi
}

# resolve_rid
assert_eq "$(resolve_rid Linux X64)"   "linux-x64"  "rid linux x64"
assert_eq "$(resolve_rid Windows ARM64)" "win-arm64" "rid win arm64"
assert_eq "$(resolve_rid macOS ARM64)" "osx-arm64"  "rid osx arm64"
assert_fail "rid rejects unknown os" resolve_rid Solaris X64

# resolve_version
assert_eq "$(resolve_version 'v2.2.0' 'v9.9.9')" "v2.2.0" "explicit version wins"
assert_eq "$(resolve_version 'latest' 'v2.3.0')" "latest" "explicit latest"
assert_eq "$(resolve_version '' 'v2.3.0')"       "v2.3.0" "default uses version-tag action_ref"
assert_eq "$(resolve_version '' 'main')"         "latest" "default falls back to latest"

# select_asset_url
REL="$(cat "$DIR/fixtures/release.json")"
assert_eq "$(select_asset_url "$REL" 'win-x64')"   "https://example/win-x64.zip"       "asset win-x64"
assert_eq "$(select_asset_url "$REL" 'osx-arm64')" "https://example/osx-arm64.tar.gz"  "asset osx-arm64"
assert_eq "$(select_asset_url "$REL" 'linux-arm64')" ""                                "asset missing -> empty"

# mode_switch
assert_eq "$(mode_switch deploy)"          ""                 "mode deploy no switch"
assert_eq "$(mode_switch validate)"        "--Validate"       "mode validate"
assert_eq "$(mode_switch preview-targets)" "--PreviewTargets" "mode preview-targets"
assert_fail "mode rejects unknown" mode_switch frobnicate

echo "---"; [ "$fails" -eq 0 ] && echo "ALL PASS" || { echo "$fails FAILED"; exit 1; }

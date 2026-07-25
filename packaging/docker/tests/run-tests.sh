#!/usr/bin/env bash
# Zero-dependency unit tests for the docker-publish resolution functions.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
source "$DIR/../docker-publish.sh" --lib-only

fails=0
assert_eq() { # $1=actual $2=expected $3=label
  if [ "$1" = "$2" ]; then echo "ok   - $3"; else echo "FAIL - $3: got '$1' want '$2'"; fails=$((fails+1)); fi
}

# derive_tags — order matters (full, major.minor, major, latest)
assert_eq "$(derive_tags 2.3.0 | paste -sd, -)" "2.3.0,2.3,2,latest" "derive_tags 2.3.0"
assert_eq "$(derive_tags 10.4.7 | paste -sd, -)" "10.4.7,10.4,10,latest" "derive_tags 10.4.7"

# image_refs — repo prefixed onto each tag
assert_eq "$(image_refs ghcr.io/schema-smith/schemaquench 2.3.0 | paste -sd, -)" \
  "ghcr.io/schema-smith/schemaquench:2.3.0,ghcr.io/schema-smith/schemaquench:2.3,ghcr.io/schema-smith/schemaquench:2,ghcr.io/schema-smith/schemaquench:latest" \
  "image_refs ghcr"
assert_eq "$(image_refs schemasmithyfree/schemaquench 2.3.0 | wc -l | tr -d ' ')" "4" "image_refs hub count"
assert_eq "$(image_refs ghcr.io/schema-smith/schemaquench 10.4.7 | paste -sd, -)" \
  "ghcr.io/schema-smith/schemaquench:10.4.7,ghcr.io/schema-smith/schemaquench:10.4,ghcr.io/schema-smith/schemaquench:10,ghcr.io/schema-smith/schemaquench:latest" \
  "image_refs ghcr 10.4.7"

# collect_tag_args — GHCR always; Docker Hub tags only when SS_PUSH_HUB=true
assert_eq "$(SS_VERSION=2.3.0 SS_PUSH_HUB=false collect_tag_args | grep -o -- '-t' | wc -l | tr -d ' ')" "4" "collect_tag_args ghcr-only = 4 tags"
assert_eq "$(SS_VERSION=2.3.0 SS_PUSH_HUB=true collect_tag_args | grep -o -- '-t' | wc -l | tr -d ' ')" "8" "collect_tag_args ghcr+hub = 8 tags"
assert_eq "$(SS_VERSION=2.3.0 SS_PUSH_HUB=true collect_tag_args | grep -c 'schemasmithyfree/schemaquench:latest')" "1" "collect_tag_args includes hub latest when enabled"
assert_eq "$(SS_VERSION=2.3.0 SS_PUSH_HUB=false collect_tag_args | grep -c 'schemasmithyfree')" "0" "collect_tag_args omits hub when disabled"

echo "---"; [ "$fails" -eq 0 ] && echo "ALL PASS" || { echo "$fails FAILED"; exit 1; }

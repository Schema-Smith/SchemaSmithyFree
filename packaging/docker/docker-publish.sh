#!/usr/bin/env bash
# SchemaQuench Docker publish — entrypoint + resolution library.
# Sourced with --lib-only by unit tests; run directly by docker-publish.yml.
set -uo pipefail

# derive_tags <version> -> newline list: full, major.minor, major, latest
derive_tags() {
  local v="$1"
  printf '%s\n%s\n%s\nlatest\n' "$v" "${v%.*}" "${v%%.*}"
}

# image_refs <repo> <version> -> "<repo>:<tag>" one per line
image_refs() {
  local repo="$1" version="$2" tag
  while IFS= read -r tag; do printf '%s:%s\n' "$repo" "$tag"; done < <(derive_tags "$version")
}

# When sourced with --lib-only, expose functions and stop (no main run).
if [ "${1:-}" = "--lib-only" ]; then return 0 2>/dev/null || exit 0; fi

main() { :; }   # filled in Task 3
main "$@"

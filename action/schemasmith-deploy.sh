#!/usr/bin/env bash
# SchemaSmith Deploy — GitHub Action entrypoint + resolution library.
# Sourced with --lib-only by unit tests; run directly by action.yml.
set -uo pipefail

resolve_rid() {
  local os="$1" arch="$2" o a
  case "$os" in
    Linux) o=linux ;; Windows) o=win ;; macOS) o=osx ;;
    *) echo "unsupported RUNNER_OS: $os" >&2; return 1 ;;
  esac
  case "$arch" in
    X64) a=x64 ;; ARM64) a=arm64 ;;
    *) echo "unsupported RUNNER_ARCH: $arch" >&2; return 1 ;;
  esac
  printf '%s-%s\n' "$o" "$a"
}

resolve_version() {
  local version_input="$1" action_ref="$2"
  if [ -n "$version_input" ]; then
    printf '%s\n' "$version_input"
  elif printf '%s' "$action_ref" | grep -qE '^v[0-9]'; then
    printf '%s\n' "$action_ref"
  else
    printf 'latest\n'
  fi
}

select_asset_url() {
  local release_json="$1" rid="$2"
  printf '%s' "$release_json" | jq -r --arg rid "$rid" \
    '.assets[] | select(.name | test("-" + $rid + "\\.(zip|tar\\.gz)$")) | .browser_download_url' \
    | head -n1
}

mode_switch() {
  case "$1" in
    deploy|whatif) printf '\n' ;;
    validate) printf -- '--Validate\n' ;;
    test-connection) printf -- '--TestConnection\n' ;;
    preview-targets) printf -- '--PreviewTargets\n' ;;
    *) echo "unsupported mode: $1" >&2; return 1 ;;
  esac
}

# When sourced with --lib-only, expose functions and stop (no main run).
if [ "${1:-}" = "--lib-only" ]; then return 0 2>/dev/null || exit 0; fi

main() { :; }   # filled in Task 2
main "$@"

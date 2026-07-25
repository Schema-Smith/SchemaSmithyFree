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

gh_api() { # $1 = url ; authenticated if GH_TOKEN present
  local auth=()
  [ -n "${GH_TOKEN:-}" ] && auth=(-H "Authorization: Bearer $GH_TOKEN")
  curl -sfL ${auth[@]+"${auth[@]}"} -H "Accept: application/vnd.github+json" "$1"
}

main() {
  local repo="Schema-Smith/SchemaSmith"
  local mode="${SS_MODE:-deploy}"
  local rid version rel_json url work bin logdir sw summary code pkg_win dest_win

  # required-input validation per mode
  if [ "$mode" != "validate" ] && [ -z "${SS_SERVER:-}" ]; then
    echo "::error::mode '$mode' requires 'server'"; exit 1
  fi
  if [ ! -e "${SS_PRODUCT_PATH:-.}" ]; then
    echo "::error::product-path '${SS_PRODUCT_PATH:-.}' does not exist"; exit 1
  fi

  rid="$(resolve_rid "$RUNNER_OS" "$RUNNER_ARCH")" || exit 1
  version="$(resolve_version "${SS_VERSION:-}" "${SS_ACTION_REF:-}")"
  echo "Resolving SchemaSmith $version for $rid"
  if [ "$version" = "latest" ]; then
    rel_json="$(gh_api "https://api.github.com/repos/$repo/releases/latest")"
  else
    rel_json="$(gh_api "https://api.github.com/repos/$repo/releases/tags/$version")"
  fi
  url="$(select_asset_url "$rel_json" "$rid")"
  [ -n "$url" ] || { echo "::error::no release asset for rid $rid (version $version)"; exit 1; }

  work="$(mktemp -d)"
  echo "Downloading $url"
  curl -sfL "$url" -o "$work/pkg" || { echo "::error::download failed: $url"; exit 1; }
  case "$url" in
    *.tar.gz) tar -xzf "$work/pkg" -C "$work" ;;
    *.zip)
      if command -v unzip >/dev/null 2>&1; then
        unzip -q "$work/pkg" -d "$work"
      else
        # PowerShell fallback (Windows without unzip): translate the git-bash POSIX
        # paths to Windows paths so Expand-Archive resolves them.
        pkg_win="$(cygpath -w "$work/pkg")"; dest_win="$(cygpath -w "$work")"
        powershell -NoProfile -Command "Expand-Archive -Path '$pkg_win' -DestinationPath '$dest_win' -Force"
      fi ;;
    *) echo "::error::unrecognized archive: $url"; exit 1 ;;
  esac

  bin="$(find "$work" -maxdepth 2 \( -name 'SchemaQuench' -o -name 'SchemaQuench.exe' \) | head -n1)"
  [ -n "$bin" ] || { echo "::error::SchemaQuench binary not found in archive"; exit 1; }
  chmod +x "$bin" 2>/dev/null || true

  logdir="$RUNNER_TEMP/schemasmith-logs"; mkdir -p "$logdir"
  sw="$(mode_switch "$mode")" || exit 1

  # config via env (password never on argv); package path + mode + extra-args
  export SmithySettings_SchemaPackagePath="${SS_PRODUCT_PATH:-.}"
  [ -n "${SS_SERVER:-}" ] && export SmithySettings_Target__Server="$SS_SERVER"
  [ -n "${SS_USER:-}" ] && export SmithySettings_Target__User="$SS_USER"
  [ -n "${SS_PASSWORD:-}" ] && export SmithySettings_Target__Password="$SS_PASSWORD"
  [ "$mode" = "whatif" ] && export SmithySettings_WhatIfONLY=true

  # shellcheck disable=SC2086  # intentional word-splitting of $sw and extra-args
  "$bin" $sw --LogPath="$logdir" ${SS_EXTRA_ARGS:-}
  code=$?

  summary=""
  [ -f "$logdir/SchemaQuench - Summary.md" ] && summary="$logdir/SchemaQuench - Summary.md"
  {
    echo "exit-code=$code"
    echo "log-dir=$logdir"
    echo "summary-path=$summary"
  } >> "$GITHUB_OUTPUT"
  exit "$code"
}
main "$@"

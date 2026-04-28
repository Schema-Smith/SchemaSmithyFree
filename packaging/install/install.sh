#!/bin/sh
# SchemaSmith install script — POSIX sh, no bash-isms.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/Schema-Smith/SchemaSmith/main/packaging/install/install.sh | sh
#
# Optional environment overrides:
#   INSTALL_VERSION  — pin to a specific SchemaSmith version (default: latest)
#   INSTALL_DIR      — install directory (default: /usr/local/bin if root, else ~/.local/bin)
#
# Installs the three SchemaSmith CLI tools (schemaquench, schematongs, datatongs)
# from the matching GitHub Release bundle for the detected OS/arch. Verifies
# SHA-256 against the release SHA256SUMS manifest before extraction.

set -eu

WORK=$(mktemp -d 2>/dev/null || mktemp -d -t schemasmith-install)
trap 'rm -rf "$WORK"' EXIT INT TERM

fail() {
  printf 'error: %s\n' "$*" >&2
  exit 1
}

info() {
  printf '%s\n' "$*"
}

main() {
  info "SchemaSmith install: starting"
  # Subsequent tasks fill in the rest.
}

main "$@"

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

detect_os() {
  case "$(uname -s)" in
    Linux)  echo linux ;;
    Darwin) echo osx ;;
    *)      fail "Unsupported OS: $(uname -s). install.sh supports Linux and macOS only. For Windows, install via Chocolatey: choco install schemasmith" ;;
  esac
}

detect_arch() {
  case "$(uname -m)" in
    x86_64|amd64)  echo x64 ;;
    aarch64|arm64) echo arm64 ;;
    *)             fail "Unsupported architecture: $(uname -m). install.sh supports x86_64 and aarch64/arm64 only." ;;
  esac
}

need_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    fail "$1 not found. Install via your package manager (e.g., 'apt install $1' on Debian/Ubuntu, 'brew install $1' on macOS)."
  fi
}

# Picks sha256sum (Linux/coreutils) or shasum -a 256 (macOS/BSD).
# Sets SHA256_CMD to the resolved command.
detect_sha256_cmd() {
  if command -v sha256sum >/dev/null 2>&1; then
    SHA256_CMD="sha256sum"
  elif command -v shasum >/dev/null 2>&1; then
    SHA256_CMD="shasum -a 256"
  else
    fail "Neither sha256sum nor shasum is available. Install GNU coreutils ('apt install coreutils', 'brew install coreutils') or perl shasum."
  fi
}

main() {
  info "SchemaSmith install: starting"
  OS=$(detect_os)
  ARCH=$(detect_arch)
  RID="${OS}-${ARCH}"
  info "Detected: ${RID}"

  need_cmd curl
  need_cmd tar
  need_cmd install
  detect_sha256_cmd
  info "Tooling: curl, tar, install, ${SHA256_CMD}"
}

main "$@"

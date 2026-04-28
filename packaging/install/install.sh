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

REPO="Schema-Smith/SchemaSmith"

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

# Resolves the version to install. If INSTALL_VERSION is set, uses it as-is.
# Otherwise follows the /releases/latest redirect to /releases/tag/vX.Y.Z and
# extracts the version. No GitHub API quota burn.
resolve_version() {
  if [ -n "${INSTALL_VERSION:-}" ]; then
    echo "$INSTALL_VERSION"
    return 0
  fi
  resolved=$(curl -fsSLI -o /dev/null -w '%{url_effective}' \
    "https://github.com/${REPO}/releases/latest" 2>/dev/null \
    | sed -n 's|.*/tag/v\([^/]*\)$|\1|p')
  if [ -z "$resolved" ]; then
    fail "Could not resolve latest version from https://github.com/${REPO}/releases/latest. Try INSTALL_VERSION=<x.y.z>."
  fi
  echo "$resolved"
}

# Resolves the install directory. INSTALL_DIR overrides; otherwise:
#   /usr/local/bin if running as root
#   ~/.local/bin   otherwise
resolve_install_dir() {
  if [ -n "${INSTALL_DIR:-}" ]; then
    echo "$INSTALL_DIR"
    return 0
  fi
  if [ "$(id -u)" -eq 0 ]; then
    echo "/usr/local/bin"
  else
    echo "${HOME}/.local/bin"
  fi
}

# Downloads the bundle tarball and SHA256SUMS for the resolved version+RID.
# Sets BUNDLE_PATH and SUMS_PATH to the local file paths in $WORK. The bundle
# filename can be recovered from BUNDLE_PATH via "${BUNDLE_PATH##*/}".
download_release() {
  base="https://github.com/${REPO}/releases/download/v${VERSION}"
  bundle="SchemaSmith-${VERSION}-${RID}.tar.gz"
  BUNDLE_PATH="${WORK}/${bundle}"
  SUMS_PATH="${WORK}/SHA256SUMS"

  info "Downloading ${bundle}"
  curl -fsSL "${base}/${bundle}" -o "${BUNDLE_PATH}" \
    || fail "Failed to download ${base}/${bundle}. Check network or pinned INSTALL_VERSION."

  info "Downloading SHA256SUMS"
  curl -fsSL "${base}/SHA256SUMS" -o "${SUMS_PATH}" \
    || fail "Failed to download ${base}/SHA256SUMS. Older SchemaSmith releases (pre-v2.0.0) did not include this manifest."
}

# Extracts the expected SHA-256 for the bundle's filename from SUMS_PATH and
# compares against the actual hash of BUNDLE_PATH. Avoids `sha256sum -c`
# because that tries to verify every entry in the manifest, including
# artifacts we did not download, and shasum -c semantics differ across
# macOS versions.
verify_sha256() {
  bundle_name="${BUNDLE_PATH##*/}"
  expected=$(awk -v f="$bundle_name" '$2==f || $2=="*"f {print $1; exit}' "$SUMS_PATH")
  if [ -z "$expected" ]; then
    fail "No SHA256 entry for ${bundle_name} in SHA256SUMS. The release manifest may be corrupted or stale."
  fi
  actual=$(${SHA256_CMD} "$BUNDLE_PATH" | awk '{print $1}')
  if [ "$expected" != "$actual" ]; then
    fail "Checksum mismatch for ${bundle_name} (expected ${expected}, got ${actual}). Re-run; if persistent, file an issue."
  fi
  info "SHA256 verified: ${expected}"
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

  VERSION=$(resolve_version)
  info "Version: ${VERSION}"

  TARGET=$(resolve_install_dir)
  info "Install dir: ${TARGET}"
  mkdir -p "$TARGET" || fail "Cannot create install dir ${TARGET}. Re-run with sudo, or set INSTALL_DIR=<writable path>."

  download_release
  verify_sha256
}

main "$@"

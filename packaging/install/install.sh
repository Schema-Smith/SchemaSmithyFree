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
TOOLS="SchemaQuench SchemaTongs DataTongs"

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

# Detects whether this Linux is glibc or musl. The .NET self-contained
# binaries we ship target glibc; musl-based distros (Alpine, etc.) need
# the .deb / .rpm packages — or a glibc base image — instead. Returns
# "glibc", "musl", or "n/a" (non-Linux).
detect_libc() {
  case "$(uname -s)" in
    Linux) ;;
    *)     echo n/a; return 0 ;;
  esac
  if ldd --version 2>&1 | grep -qi musl; then
    echo musl
  else
    # Treat anything that is not unambiguously musl as glibc. Worst case
    # we fall through to a runtime "Failed to load app-local ICU" error,
    # which is exactly the behavior before this detection existed.
    echo glibc
  fi
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

# Extracts the verified bundle to a staging directory, then installs each
# tool to TARGET with a lowercase name (matching the .deb/.rpm convention).
extract_and_install() {
  extract_dir="${WORK}/extract"
  mkdir -p "$extract_dir"
  bundle_name="${BUNDLE_PATH##*/}"
  tar -xzf "$BUNDLE_PATH" -C "$extract_dir" \
    || fail "Failed to extract ${bundle_name} — possibly a corrupt download."

  for tool in $TOOLS; do
    src="${extract_dir}/${tool}"
    if [ ! -f "$src" ]; then
      fail "Bundle is missing ${tool}. Re-run; if persistent, file an issue."
    fi
    lower=$(printf '%s' "$tool" | tr '[:upper:]' '[:lower:]')
    dest="${TARGET}/${lower}"
    install -m 0755 "$src" "$dest" \
      || fail "Cannot write ${dest}. Re-run with sudo, or set INSTALL_DIR=<writable path>."
    info "Installed ${dest}"
  done

  # Linux RIDs bundle three ICU shared libraries (libicudata, libicui18n,
  # libicuuc — version 72.1.0.3 as of v2.0.0) that the .NET AppLocalIcu
  # host config loads at startup. They MUST be installed alongside the
  # binaries in TARGET or every CLI invocation fails with
  # "Failed to load app-local ICU: libicudata.so.<ver>". macOS RIDs use
  # system libicu and ship no libicu*.so.* files, so this loop is a
  # no-op on Darwin.
  for lib in "${extract_dir}"/libicu*.so.*; do
    [ -e "$lib" ] || continue
    install -m 0644 "$lib" "${TARGET}/${lib##*/}" \
      || fail "Cannot write ${TARGET}/${lib##*/}. Re-run with sudo, or set INSTALL_DIR=<writable path>."
    info "Installed ${TARGET}/${lib##*/}"
  done
}

# Returns 0 if TARGET is on $PATH, 1 otherwise.
target_on_path() {
  case ":${PATH}:" in
    *":${TARGET}:"*) return 0 ;;
    *)               return 1 ;;
  esac
}

# Prints shell-specific PATH-fixup instructions for TARGET.
# shellcheck disable=SC2016
# The $PATH inside the single-quoted strings below is intentional: we are
# emitting the literal text the user should append to their rc file, where
# $PATH must remain unexpanded so the user's shell expands it at .bashrc
# evaluation time (not at install-script evaluation time).
print_path_fix() {
  shell_name=$(basename "${SHELL:-/bin/bash}")
  printf '\n%s is not on PATH. Add it:\n\n' "$TARGET"
  case "$shell_name" in
    bash)
      printf '  echo '\''export PATH="%s:$PATH"'\'' >> ~/.bashrc\n' "$TARGET"
      printf '  source ~/.bashrc\n' ;;
    zsh)
      printf '  echo '\''export PATH="%s:$PATH"'\'' >> ~/.zshrc\n' "$TARGET"
      printf '  source ~/.zshrc\n' ;;
    fish)
      printf '  fish_add_path %s\n' "$TARGET" ;;
    *)
      printf '  # bash:\n'
      printf '  echo '\''export PATH="%s:$PATH"'\'' >> ~/.bashrc\n\n' "$TARGET"
      printf '  # zsh:\n'
      printf '  echo '\''export PATH="%s:$PATH"'\'' >> ~/.zshrc\n' "$TARGET" ;;
  esac
}

# Prints the success summary and uninstall one-liner.
print_success() {
  printf '\nSchemaSmith %s installed to %s\n' "$VERSION" "$TARGET"
  printf '  schemaquench --version\n'
  printf '  schematongs --version\n'
  printf '  datatongs --version\n'
  if ! target_on_path; then
    print_path_fix
  fi
  printf '\nTo uninstall: rm -f %s/schemaquench %s/schematongs %s/datatongs %s/libicu*.so.*\n' \
    "$TARGET" "$TARGET" "$TARGET" "$TARGET"
}

main() {
  info "SchemaSmith install: starting"
  OS=$(detect_os)
  ARCH=$(detect_arch)
  RID="${OS}-${ARCH}"
  info "Detected: ${RID}"

  LIBC=$(detect_libc)
  if [ "$OS" = "linux" ] && [ "$LIBC" = "musl" ]; then
    fail "install.sh ships glibc-based Linux binaries; this system uses musl libc (typically Alpine Linux). The .NET self-contained binaries we publish target glibc and won't run on a musl loader. Options: (1) install via the .deb / .rpm packages on a glibc-based distro (Debian, Ubuntu, RHEL, Fedora, Amazon Linux), (2) use a glibc base image instead of an Alpine base, or (3) file an issue requesting a linux-musl-x64 build. Until then, install.sh on Alpine is unsupported."
  fi

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
  extract_and_install
  print_success
}

main "$@"

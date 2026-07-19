#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
MANIFEST="$HERE/demo-databases.manifest"; FORCE=0; USER_=""; PASSWORD=""
while [ $# -gt 0 ]; do case "$1" in
  --server) SERVER="$2"; shift 2;; --user) USER_="$2"; shift 2;;
  --password) PASSWORD="$2"; shift 2;; --manifest) MANIFEST="$2"; shift 2;;
  --force) FORCE=1; shift;; *) echo "unknown arg: $1" >&2; exit 64;; esac; done

# Omit --user/--password to connect with Windows Authentication (trusted connection);
# SQL Server only. Supplying a user requires a password.
if [ -n "$USER_" ] && [ -z "$PASSWORD" ]; then
  echo "--user was supplied without --password. Provide both for SQL auth, or omit both for Windows Authentication." >&2
  exit 1
fi

command -v sqlcmd >/dev/null 2>&1 || { cat >&2 <<'EOF'
sqlcmd is required but was not found on PATH.
Install the SQL Server command-line tools and re-open your shell:
  macOS : brew install sqlcmd
  Linux : follow https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility (mssql-tools18)
Verify with:  sqlcmd -?
EOF
exit 1; }

command -v schemaquench >/dev/null 2>&1 || { echo "schemaquench is required but was not found on PATH." >&2; exit 1; }

sql() { # sql <infile> [-v NAME=VAL ...]
  local infile="$1"; shift
  # Under Git Bash / Cygwin on Windows, sqlcmd is a native .exe that needs a
  # Windows path; cygpath is absent on macOS/Linux, where the POSIX path is used.
  command -v cygpath >/dev/null 2>&1 && infile="$(cygpath -w "$infile")"
  local auth
  if [ -n "$USER_" ]; then auth=(-U "$USER_" -P "$PASSWORD"); else auth=(-E); fi
  sqlcmd -S "$SERVER" "${auth[@]}" -C -b -h -1 -W -i "$infile" "$@"
}

# read manifest -> arrays
NAMES=(); TYPES=(); TOKENS=(); PKGS=()
while IFS='|' read -r type name token pkg; do
  [[ -z "$type" || "$type" =~ ^[[:space:]]*# ]] && continue
  TYPES+=("$type"); NAMES+=("$name"); TOKENS+=("$token"); PKGS+=("$pkg")
done < "$MANIFEST"

TO_DROP=(); COLLIDE=()
for i in "${!NAMES[@]}"; do
  res="$(sql "$HERE/endpoint/stamp.sql" -v Op=check -v Db="${NAMES[$i]}")"
  case "$res" in *STAMP_RESULT:stamped*) TO_DROP+=("${NAMES[$i]}");;
                 *STAMP_RESULT:unstamped*) COLLIDE+=("${NAMES[$i]}");; esac
done
if [ "${#COLLIDE[@]}" -gt 0 ]; then
  echo "These databases already exist on $SERVER but were NOT created by this helper:" >&2
  echo "  ${COLLIDE[*]}" >&2
  echo "Rename the colliding entries in $MANIFEST, then re-run." >&2; exit 2
fi
if [ "${#TO_DROP[@]}" -gt 0 ] && [ "$FORCE" -ne 1 ]; then
  echo "WILL DROP and recreate on $SERVER: ${TO_DROP[*]}"
  read -r -p "Type 'yes' to continue: " ans; [ "$ans" = yes ] || { echo Aborted; exit 0; }
fi
for name in "${TO_DROP[@]:-}"; do [ -n "$name" ] && sql "$HERE/endpoint/stamp.sql" -v Op=dropIfStamped -v Db="$name" >/dev/null; done
sql "$HERE/endpoint/bootstrap.sql" >/dev/null

for i in "${!NAMES[@]}"; do
  [ "${TYPES[$i]}" = product ] || continue
  # Configure via SmithySettings_* env vars (the form the Docker demo uses), NOT --CLI
  # overrides: the generic `--Key=value` override is newer than some released SchemaQuench
  # builds, so a --SchemaPackagePath override is silently ignored on an older installed CLI.
  # SchemaPackagePath points at this package; the ScriptTokens override renames the deployed
  # DB when the manifest NAME differs from the package default (collision workaround), and is
  # a harmless no-op when NAME matches.
  ( cd "$HERE/${PKGS[$i]}"
    export SmithySettings_Target__Server="$SERVER" SmithySettings_Target__User="$USER_" \
           SmithySettings_Target__Password="$PASSWORD" \
           SmithySettings_Target__ConnectionProperties__TrustServerCertificate=True \
           SmithySettings_SchemaPackagePath="$HERE/${PKGS[$i]}" \
           "SmithySettings_ScriptTokens__${TOKENS[$i]}=${NAMES[$i]}"
    schemaquench ) \
    || { echo "Quench failed for ${NAMES[$i]}" >&2; exit 1; }
  sql "$HERE/endpoint/stamp.sql" -v Op=add -v Db="${NAMES[$i]}" >/dev/null
done
echo "Done. Demo databases provisioned on $SERVER."

#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
MANIFEST="$HERE/demo-databases.manifest"; FORCE=0; SERVER=""; PORT=5432; USER_=""; PASSWORD=""
while [ $# -gt 0 ]; do case "$1" in
  --server) SERVER="$2"; shift 2;; --port) PORT="$2"; shift 2;;
  --user) USER_="$2"; shift 2;; --password) PASSWORD="$2"; shift 2;;
  --manifest) MANIFEST="$2"; shift 2;; --force) FORCE=1; shift;;
  *) echo "unknown arg: $1" >&2; exit 64;; esac; done

command -v psql >/dev/null 2>&1 || { cat >&2 <<'EOF'
psql is required but was not found on PATH.
Install the PostgreSQL client tools and re-open your shell:
  macOS : brew install libpq   (then add its bin to PATH) or brew install postgresql
  Linux : apt-get install postgresql-client   (or dnf install postgresql)
Verify with:  psql --version
EOF
exit 1; }
command -v schemaquench >/dev/null 2>&1 || { echo "schemaquench is required but was not found on PATH." >&2; exit 1; }

# psql authenticates from PGPASSWORD; admin runs connect to the 'postgres'
# maintenance database (you cannot CREATE/DROP the database you are connected to).
export PGPASSWORD="$PASSWORD"

psql_run() { # psql_run <infile> [-v NAME=VAL ...]
  local infile="$1"; shift
  # psql on Windows (Git Bash) accepts POSIX paths, so no cygpath translation needed.
  psql -h "$SERVER" -p "$PORT" -U "$USER_" -d postgres -w \
       -v ON_ERROR_STOP=1 --no-psqlrc -t -A -f "$infile" "$@"
}

# read manifest -> arrays
NAMES=(); TYPES=(); TOKENS=(); PKGS=()
while IFS='|' read -r type name token pkg; do
  [[ -z "$type" || "$type" =~ ^[[:space:]]*# ]] && continue
  TYPES+=("$type"); NAMES+=("$name"); TOKENS+=("$token"); PKGS+=("$pkg")
done < "$MANIFEST"

TO_DROP=(); COLLIDE=()
for i in "${!NAMES[@]}"; do
  res="$(psql_run "$HERE/endpoint/stamp.sql" -v op=check -v db="${NAMES[$i]}")"
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
for name in "${TO_DROP[@]:-}"; do [ -n "$name" ] && psql_run "$HERE/endpoint/stamp.sql" -v op=dropIfStamped -v db="$name" >/dev/null; done
psql_run "$HERE/endpoint/bootstrap.sql" >/dev/null

for i in "${!NAMES[@]}"; do
  [ "${TYPES[$i]}" = product ] || continue
  # Configure via SmithySettings_* env vars (the form the Docker demo uses), NOT --CLI
  # overrides: the generic `--Key=value` override is newer than some released SchemaQuench
  # builds, so a --SchemaPackagePath override is silently ignored on an older installed CLI.
  # SchemaPackagePath points at this package; the ScriptTokens override renames the deployed
  # DB when the manifest NAME differs from the package default (collision workaround), and is
  # a harmless no-op when NAME matches.
  ( cd "$HERE/${PKGS[$i]}"
    export SmithySettings_Target__Server="$SERVER" SmithySettings_Target__Port="$PORT" \
           SmithySettings_Target__User="$USER_" SmithySettings_Target__Password="$PASSWORD" \
           SmithySettings_SchemaPackagePath="$HERE/${PKGS[$i]}" \
           "SmithySettings_ScriptTokens__${TOKENS[$i]}=${NAMES[$i]}"
    schemaquench ) \
    || { echo "Quench failed for ${NAMES[$i]}" >&2; exit 1; }
  psql_run "$HERE/endpoint/stamp.sql" -v op=add -v db="${NAMES[$i]}" >/dev/null
done
echo "Done. Demo databases provisioned on $SERVER."

#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
MANIFEST="$HERE/demo-databases.manifest"; FORCE=0; SERVER=""; PORT=3306; USER_=""; PASSWORD=""
while [ $# -gt 0 ]; do case "$1" in
  --server) SERVER="$2"; shift 2;; --port) PORT="$2"; shift 2;;
  --user) USER_="$2"; shift 2;; --password) PASSWORD="$2"; shift 2;;
  --manifest) MANIFEST="$2"; shift 2;; --force) FORCE=1; shift;;
  *) echo "unknown arg: $1" >&2; exit 64;; esac; done

# The MariaDB client is `mariadb` (`mysql` is a legacy symlink being phased out).
command -v mariadb >/dev/null 2>&1 || { cat >&2 <<'EOF'
mariadb is required but was not found on PATH.
Install the MariaDB client tools and re-open your shell:
  macOS : brew install mariadb   (then add its bin to PATH)
  Linux : apt-get install mariadb-client   (or dnf install MariaDB-client)
Verify with:  mariadb --version
EOF
exit 1; }
command -v schemaquench >/dev/null 2>&1 || { echo "schemaquench is required but was not found on PATH." >&2; exit 1; }

# mariadb reads the password from MYSQL_PWD (avoids the "password on the command line
# is insecure" warning). Connect with NO default database so admin ops work even when
# the target database does not exist yet.
export MYSQL_PWD="$PASSWORD"

maria_run() { # maria_run <infile> [<db> <op>]
  local infile="$1"; local db="${2:-}"; local op="${3:-}"
  if [ -n "$db" ]; then
    mariadb -h "$SERVER" -P "$PORT" -u "$USER_" -N -s --init-command="SET @db='$db', @op='$op'" < "$infile"
  else
    mariadb -h "$SERVER" -P "$PORT" -u "$USER_" -N -s < "$infile"
  fi
}

# read manifest -> arrays
NAMES=(); TYPES=(); TOKENS=(); PKGS=()
while IFS='|' read -r type name token pkg; do
  [[ -z "$type" || "$type" =~ ^[[:space:]]*# ]] && continue
  TYPES+=("$type"); NAMES+=("$name"); TOKENS+=("$token"); PKGS+=("$pkg")
done < "$MANIFEST"

TO_DROP=(); COLLIDE=()
for i in "${!NAMES[@]}"; do
  res="$(maria_run "$HERE/endpoint/stamp.sql" "${NAMES[$i]}" check)"
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
for name in "${TO_DROP[@]:-}"; do [ -n "$name" ] && maria_run "$HERE/endpoint/stamp.sql" "$name" dropIfStamped >/dev/null; done
maria_run "$HERE/endpoint/bootstrap.sql" >/dev/null

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
  maria_run "$HERE/endpoint/stamp.sql" "${NAMES[$i]}" add >/dev/null
done
echo "Done. Demo databases provisioned on $SERVER."

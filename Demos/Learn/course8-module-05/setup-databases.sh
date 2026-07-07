#!/usr/bin/env bash
# Create the Course 8 Module 5 recovery-toolkit sandbox on each sandbox engine: ONE EMPTY
# database diag_recovery on SQL Server, PostgreSQL, and MySQL. No schema is seeded —
# later tasks deploy a schema into it and break it on purpose. Re-running is
# safe; all CREATE DDL is guarded. PASS is reported only after the database
# is confirmed to exist on an engine.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
fail=0

apply_seed() {
  local engine="$1" file="$SCRIPT_DIR/seed/$1/01_create_recovery_database.sql"
  if [ ! -f "$file" ]; then echo "  MISSING $file"; fail=1; return 1; fi
  # Copy the script into the container and run it there rather than piping on stdin:
  # keeps behaviour identical across bash, Windows PowerShell 5.1 (which injects a
  # UTF-8 BOM sqlcmd rejects on piped input), and pwsh 7.
  case "$engine" in
    sqlserver) docker cp "$file" learn-sqlserver:/tmp/seed.sql >/dev/null; docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -i /tmp/seed.sql >/dev/null 2>&1 ;;
    postgres)  docker cp "$file" learn-postgres:/tmp/seed.sql >/dev/null; docker exec learn-postgres psql -U postgres -v ON_ERROR_STOP=1 -f /tmp/seed.sql >/dev/null 2>&1 ;;
    mysql)     docker cp "$file" learn-mysql:/tmp/seed.sql >/dev/null; docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e 'source /tmp/seed.sql' >/dev/null 2>&1 ;;
  esac
}

count_scripts() {
  local engine="$1"
  case "$engine" in
    sqlserver) docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -Q \"SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'diag_recovery'\"" 2>/dev/null | tr -d '[:space:]' ;;
    postgres)  docker exec learn-postgres psql -U postgres -tAc "SELECT COUNT(*) FROM pg_database WHERE datname = 'diag_recovery'" 2>/dev/null | tr -d '[:space:]' ;;
    mysql)     docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'diag_recovery'" 2>/dev/null | tr -d '[:space:]' ;;
  esac
}

seed_engine() {
  local engine="$1" label="$2"
  printf '%-12s ' "$label"
  apply_seed "$engine"
  local n; n="$(count_scripts "$engine")"
  if [ "$n" = "1" ]; then echo "PASS (recovery-toolkit sandbox database ready)"; else echo "FAIL (found '${n}', expected 1)"; fail=1; fi
}

seed_engine sqlserver "SQL Server"
seed_engine postgres  "PostgreSQL"
seed_engine mysql     "MySQL"

echo
if [ "$fail" -eq 0 ]; then
  echo "recovery-toolkit sandbox database ready on all 3 engines — empty, ready for schema deploy."
  exit 0
else
  echo "One or more engines could not be set up. Is the sandbox up? See Demos/Learn/README.md."
  exit 1
fi

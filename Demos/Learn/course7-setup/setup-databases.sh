#!/usr/bin/env bash
# Create the Course 7 tenant fleet on each sandbox engine: five EMPTY databases
# fleet_tenant_001..005 on SQL Server, PostgreSQL, and MySQL. No schema is seeded —
# the Module 1 deploy is what forges the Shop schema into each tenant. Re-running is
# safe; all CREATE DDL is guarded. PASS is reported only after the five databases
# are confirmed to exist on an engine.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
fail=0

apply_seed() {
  local engine="$1" file="$SCRIPT_DIR/seed/$1/01_create_tenant_databases.sql"
  if [ ! -f "$file" ]; then echo "  MISSING $file"; fail=1; return 1; fi
  case "$engine" in
    sqlserver) docker exec -i learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b" < "$file" >/dev/null 2>&1 ;;
    postgres)  docker exec -i learn-postgres psql -U postgres -v ON_ERROR_STOP=1 < "$file" >/dev/null 2>&1 ;;
    mysql)     docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd < "$file" >/dev/null 2>&1 ;;
  esac
}

count_tenants() {
  local engine="$1"
  case "$engine" in
    sqlserver) docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -Q \"SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name LIKE 'fleet[_]tenant[_]%'\"" 2>/dev/null | tr -d '[:space:]' ;;
    postgres)  docker exec learn-postgres psql -U postgres -tAc "SELECT COUNT(*) FROM pg_database WHERE datname LIKE 'fleet\_tenant\_%'" 2>/dev/null | tr -d '[:space:]' ;;
    mysql)     docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name LIKE 'fleet\_tenant\_%'" 2>/dev/null | tr -d '[:space:]' ;;
  esac
}

seed_engine() {
  local engine="$1" label="$2"
  printf '%-12s ' "$label"
  apply_seed "$engine"
  local n; n="$(count_tenants "$engine")"
  if [ "$n" = "5" ]; then echo "PASS (5 tenant databases)"; else echo "FAIL (found '${n}', expected 5)"; fail=1; fi
}

seed_engine sqlserver "SQL Server"
seed_engine postgres  "PostgreSQL"
seed_engine mysql     "MySQL"

echo
if [ "$fail" -eq 0 ]; then
  echo "All 15 tenant databases created (5 SQL Server, 5 PostgreSQL, 5 MySQL) — empty, ready for Module 1."
  exit 0
else
  echo "One or more engines could not be set up. Is the sandbox up? See Demos/Learn/README.md."
  exit 1
fi

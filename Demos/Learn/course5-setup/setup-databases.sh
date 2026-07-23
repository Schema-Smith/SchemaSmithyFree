#!/usr/bin/env bash
# Create + seed the Course 5 migration-track databases on each sandbox engine.
# Each database is the post-migration state a source tool would have left behind:
# the shared shop schema plus that tool's own bookkeeping table. Re-running is
# safe — all DDL is idempotent. PASS is reported only after a shop table is
# confirmed to exist (a seed that silently fails reports FAIL, never a false PASS).
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
fail=0

# Run a .sql file's contents into a database via the engine's client over stdin.
# sqlcmd is wrapped in `bash -c` so Git Bash on Windows doesn't rewrite /opt/...
apply_sql() {
  local engine="$1" db="$2" file="$3"
  if [ ! -f "$file" ]; then echo "    MISSING $file"; fail=1; return 1; fi
  case "$engine" in
    sqlserver) docker exec -i learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -d ${db}" < "$file" >/dev/null 2>&1 ;;
    postgres)  docker exec -i learn-postgres psql -U postgres -d "${db}" -v ON_ERROR_STOP=1 < "$file" >/dev/null 2>&1 ;;
    mysql)     docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd "${db}" < "$file" >/dev/null 2>&1 ;;
    mariadb)   docker exec -i learn-mariadb mariadb -uroot -pLearn!Passw0rd "${db}" < "$file" >/dev/null 2>&1 ;;
  esac
}

# create_db + apply shop + (optional) tracker + confirm a shop table exists.
seed_db() {
  local engine="$1" db="$2" tracker="$3" out=""
  printf '  %-26s ' "$db"
  case "$engine" in
    sqlserver) docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q \"IF DB_ID('${db}') IS NULL CREATE DATABASE [${db}]\"" >/dev/null 2>&1 ;;
    postgres)  docker exec learn-postgres psql -U postgres -d postgres -c "CREATE DATABASE ${db}" >/dev/null 2>&1 ;;
    mysql)     docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS \`${db}\`" >/dev/null 2>&1 ;;
    mariadb)   docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS \`${db}\`" >/dev/null 2>&1 ;;
  esac
  apply_sql "$engine" "$db" "$SCRIPT_DIR/seed/$engine/shop.sql"
  if [ -n "$tracker" ]; then apply_sql "$engine" "$db" "$SCRIPT_DIR/seed/$engine/$tracker.sql"; fi
  case "$engine" in
    sqlserver) out=$(docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d ${db} -Q \"SELECT 'READY' WHERE OBJECT_ID('dbo.Customer') IS NOT NULL\"" 2>/dev/null) ;;
    postgres)  out=$(docker exec learn-postgres psql -U postgres -d "${db}" -tAc "SELECT 1 FROM information_schema.tables WHERE table_name='customer'" 2>/dev/null) ;;
    mysql)     out=$(docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.tables WHERE table_schema='${db}' AND table_name='Customer'" 2>/dev/null) ;;
    mariadb)   out=$(docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.tables WHERE table_schema='${db}' AND table_name='Customer'" 2>/dev/null) ;;
  esac
  if echo "$out" | grep -qE 'READY|1'; then echo "PASS"; else echo "FAIL"; fail=1; fi
}

# database : tracker-file-stem  (empty tracker = no bookkeeping table, e.g. DACPAC)
three_engine_dbs=(
  "shop_from_flyway:tracker_flyway"
  "shop_from_liquibase:tracker_liquibase"
  "shop_from_efcore:tracker_efcore"
  "shop_from_scripts:tracker_scripts"
)

echo "SQL Server"
for pair in "${three_engine_dbs[@]}"; do seed_db sqlserver "${pair%%:*}" "${pair##*:}"; done
# DACPAC is a SQL Server technology (Course 5 Module 4 is SQL-Server-only by design); no tracker table.
seed_db sqlserver shop_from_dacpac ""

echo "PostgreSQL"
for pair in "${three_engine_dbs[@]}"; do seed_db postgres "${pair%%:*}" "${pair##*:}"; done

echo "MySQL"
for pair in "${three_engine_dbs[@]}"; do seed_db mysql "${pair%%:*}" "${pair##*:}"; done

echo "MariaDB"
for pair in "${three_engine_dbs[@]}"; do seed_db mariadb "${pair%%:*}" "${pair##*:}"; done

echo
if [ "$fail" -eq 0 ]; then
  echo "All 17 databases are seeded and ready (5 SQL Server, 4 PostgreSQL, 4 MySQL, 4 MariaDB)."
  exit 0
else
  echo "One or more databases could not be seeded. Is the sandbox up? See Demos/Learn/README.md."
  exit 1
fi

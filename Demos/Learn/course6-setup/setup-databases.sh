#!/usr/bin/env bash
# Create + seed the Course 6 datafix tenant databases on each sandbox engine.
# Creates shop_tenant_a, shop_tenant_b, shop_tenant_c on SQL Server, PostgreSQL,
# and MySQL. Each database has the identical Shop schema and a deterministic
# price-defect batch: OrderItems on May-2026 SalesOrders carry UnitPrice = ROUND(
# Product.UnitPrice * 0.81, 2) — a 10% discount applied twice. All other OrderItems
# carry the intended ROUND(Product.UnitPrice * 0.90, 2). Re-running is safe — all
# DDL is idempotent. PASS is reported only after a shop table is confirmed to exist.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
fail=0

# Run a .sql file's contents into a database via the engine's client over stdin.
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

# create_db + apply shop seed + confirm a shop table exists.
seed_db() {
  local engine="$1" db="$2" out=""
  printf '  %-26s ' "$db"
  case "$engine" in
    sqlserver) docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q \"IF DB_ID('${db}') IS NULL CREATE DATABASE [${db}]\"" >/dev/null 2>&1 ;;
    postgres)  docker exec learn-postgres psql -U postgres -d postgres -c "CREATE DATABASE ${db}" >/dev/null 2>&1 ;;
    mysql)     docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS \`${db}\`" >/dev/null 2>&1 ;;
    mariadb)   docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS \`${db}\`" >/dev/null 2>&1 ;;
  esac
  apply_sql "$engine" "$db" "$SCRIPT_DIR/seed/$engine/shop.sql"
  case "$engine" in
    sqlserver) out=$(docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d ${db} -Q \"SELECT 'READY' WHERE OBJECT_ID('dbo.Customer') IS NOT NULL\"" 2>/dev/null) ;;
    postgres)  out=$(docker exec learn-postgres psql -U postgres -d "${db}" -tAc "SELECT 1 FROM information_schema.tables WHERE table_name='customer'" 2>/dev/null) ;;
    mysql)     out=$(docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.tables WHERE table_schema='${db}' AND table_name='Customer'" 2>/dev/null) ;;
    mariadb)   out=$(docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.tables WHERE table_schema='${db}' AND table_name='Customer'" 2>/dev/null) ;;
  esac
  if echo "$out" | grep -qE 'READY|1'; then echo "PASS"; else echo "FAIL"; fail=1; fi
}

# Apply the scoped datafix_user role for an engine. One invocation handles all
# three tenants; run AFTER the tenant tables exist (PostgreSQL grants ON ALL
# TABLES only cover tables present at grant time). Idempotent.
apply_role() {
  local engine="$1" out=""
  local file="$SCRIPT_DIR/seed/$engine/datafix_role.sql"
  printf '  %-26s ' "datafix_user role"
  if [ ! -f "$file" ]; then echo "MISSING"; fail=1; return 1; fi
  case "$engine" in
    sqlserver) docker exec -i learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -d master" < "$file" >/dev/null 2>&1
               out=$(docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d master -Q \"SELECT 'READY' FROM sys.server_principals WHERE name='datafix_user'\"" 2>/dev/null) ;;
    postgres)  docker exec -i learn-postgres psql -U postgres -d postgres < "$file" >/dev/null 2>&1
               out=$(docker exec learn-postgres psql -U postgres -d postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='datafix_user'" 2>/dev/null) ;;
    mysql)     docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd < "$file" >/dev/null 2>&1
               out=$(docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM mysql.user WHERE user='datafix_user'" 2>/dev/null) ;;
    mariadb)   docker exec -i learn-mariadb mariadb -uroot -pLearn!Passw0rd < "$file" >/dev/null 2>&1
               out=$(docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM mysql.user WHERE user='datafix_user'" 2>/dev/null) ;;
  esac
  if echo "$out" | grep -qE 'READY|1'; then echo "PASS"; else echo "FAIL"; fail=1; fi
}

tenants=("shop_tenant_a" "shop_tenant_b" "shop_tenant_c")

echo "SQL Server"
for db in "${tenants[@]}"; do seed_db sqlserver "$db"; done
apply_role sqlserver

echo "PostgreSQL"
for db in "${tenants[@]}"; do seed_db postgres "$db"; done
apply_role postgres

echo "MySQL"
for db in "${tenants[@]}"; do seed_db mysql "$db"; done
apply_role mysql

echo "MariaDB"
for db in "${tenants[@]}"; do seed_db mariadb "$db"; done
apply_role mariadb

echo
if [ "$fail" -eq 0 ]; then
  echo "All 12 databases are seeded and the datafix_user role is created (3 SQL Server, 3 PostgreSQL, 3 MySQL, 3 MariaDB)."
  exit 0
else
  echo "One or more databases could not be seeded. Is the sandbox up? See Demos/Learn/README.md."
  exit 1
fi

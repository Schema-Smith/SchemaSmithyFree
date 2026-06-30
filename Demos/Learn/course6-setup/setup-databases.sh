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
  esac
  apply_sql "$engine" "$db" "$SCRIPT_DIR/seed/$engine/shop.sql"
  case "$engine" in
    sqlserver) out=$(docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d ${db} -Q \"SELECT 'READY' WHERE OBJECT_ID('dbo.Customer') IS NOT NULL\"" 2>/dev/null) ;;
    postgres)  out=$(docker exec learn-postgres psql -U postgres -d "${db}" -tAc "SELECT 1 FROM information_schema.tables WHERE table_name='customer'" 2>/dev/null) ;;
    mysql)     out=$(docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.tables WHERE table_schema='${db}' AND table_name='Customer'" 2>/dev/null) ;;
  esac
  if echo "$out" | grep -qE 'READY|1'; then echo "PASS"; else echo "FAIL"; fail=1; fi
}

tenants=("shop_tenant_a" "shop_tenant_b" "shop_tenant_c")

echo "SQL Server"
for db in "${tenants[@]}"; do seed_db sqlserver "$db"; done

echo "PostgreSQL"
for db in "${tenants[@]}"; do seed_db postgres "$db"; done

echo "MySQL"
for db in "${tenants[@]}"; do seed_db mysql "$db"; done

echo
if [ "$fail" -eq 0 ]; then
  echo "All 9 databases are seeded and ready (3 SQL Server, 3 PostgreSQL, 3 MySQL)."
  exit 0
else
  echo "One or more databases could not be seeded. Is the sandbox up? See Demos/Learn/README.md."
  exit 1
fi

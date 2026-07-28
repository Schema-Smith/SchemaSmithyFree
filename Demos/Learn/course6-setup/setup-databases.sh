#!/usr/bin/env bash
# Create + seed the Course 6 datafix tenant databases on each sandbox engine, or on your own
# server's single activated engine (LEARN_SERVER). Creates shop_tenant_a, shop_tenant_b,
# shop_tenant_c. Each database has the identical Shop schema and a deterministic price-defect
# batch: OrderItems on May-2026 SalesOrders carry UnitPrice = ROUND(Product.UnitPrice * 0.81, 2)
# -- a 10% discount applied twice. All other OrderItems carry the intended ROUND(Product.UnitPrice
# * 0.90, 2). Re-running is safe -- all DDL is idempotent. PASS is reported only after a shop
# table is confirmed to exist.
#
# --reset drops and recreates the tenant databases empty before reseeding. Only databases the
# labs created are ever dropped -- see lab_remove_db.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../lab-sql.sh"

reset=0
[ "${1:-}" = "--reset" ] && reset=1

fail=0

label() {
  case "$1" in
    sqlserver) printf 'SQL Server' ;;
    postgres)  printf 'PostgreSQL' ;;
    mysql)     printf 'MySQL' ;;
    mariadb)   printf 'MariaDB' ;;
  esac
}

# create/reset + seed shop + confirm a shop table exists.
init_db() {
  local engine="$1" db="$2" err out ready_sql removed

  printf '  %-26s ' "$db"

  if [ "$reset" -eq 1 ]; then
    removed="$(lab_remove_db "$engine" "$db" 2>/dev/null)"
    if [ "$removed" = "refused" ]; then
      echo "FAIL"
      echo "    '$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
      fail=1
      return 1
    fi
  fi

  err="$(lab_confirm_db "$engine" "$db" 2>&1 1>/dev/null)"
  if [ $? -ne 0 ]; then echo "FAIL"; echo "$err" | sed 's/^/    /'; fail=1; return 1; fi

  err="$(lab_sql_file "$engine" "$db" "$SCRIPT_DIR/seed/$engine/shop.sql" 2>&1)"
  if [ $? -ne 0 ]; then echo "FAIL"; echo "$err" | sed 's/^/    /'; fail=1; return 1; fi

  case "$engine" in
    sqlserver) ready_sql="SELECT 'READY' WHERE OBJECT_ID('dbo.Customer') IS NOT NULL" ;;
    postgres)  ready_sql="SELECT 1 FROM information_schema.tables WHERE table_name='customer'" ;;
    *)         ready_sql="SELECT 1 FROM information_schema.tables WHERE table_schema='$db' AND table_name='Customer'" ;;
  esac
  out="$(lab_sql "$engine" "$db" "$ready_sql" 2>&1)"
  if [ $? -ne 0 ]; then echo "FAIL"; echo "$out" | sed 's/^/    /'; fail=1; return 1; fi
  if ! echo "$out" | grep -qE 'READY|1'; then
    echo "FAIL"
    echo "    seed completed but the shop schema's Customer table was not found."
    fail=1
    return 1
  fi

  if [ "$reset" -eq 1 ]; then echo "PASS (reset)"; else echo "PASS"; fi
}

# Apply the scoped datafix_user role for an engine. One invocation handles all three
# tenants; run AFTER the tenant tables exist (PostgreSQL grants ON ALL TABLES only cover
# tables present at grant time). Idempotent.
apply_role() {
  local engine="$1" admin err out ready_sql

  printf '  %-26s ' "datafix_user role"
  admin="$(lab_admin_db "$engine")"

  err="$(lab_sql_file "$engine" "$admin" "$SCRIPT_DIR/seed/$engine/datafix_role.sql" 2>&1)"
  if [ $? -ne 0 ]; then echo "FAIL"; echo "$err" | sed 's/^/    /'; fail=1; return 1; fi

  case "$engine" in
    sqlserver) ready_sql="SELECT 'READY' FROM sys.server_principals WHERE name='datafix_user'" ;;
    postgres)  ready_sql="SELECT 1 FROM pg_roles WHERE rolname='datafix_user'" ;;
    *)         ready_sql="SELECT 1 FROM mysql.user WHERE user='datafix_user'" ;;
  esac
  out="$(lab_sql "$engine" "$admin" "$ready_sql" 2>&1)"
  if [ $? -ne 0 ]; then echo "FAIL"; echo "$out" | sed 's/^/    /'; fail=1; return 1; fi
  if ! echo "$out" | grep -qE 'READY|1'; then
    echo "FAIL"
    echo "    datafix_role.sql ran but the datafix_user role/login was not found."
    fail=1
    return 1
  fi

  echo "PASS"
}

tenants=(shop_tenant_a shop_tenant_b shop_tenant_c)

engines="$(lab_engines)" || exit 1
total=0
parts=""
for engine in $engines; do
  echo "$(label "$engine")"
  n=0
  for db in "${tenants[@]}"; do
    if init_db "$engine" "$db"; then n=$((n + 1)); fi
  done
  apply_role "$engine"
  total=$((total + n))
  parts="${parts}${parts:+, }${n} $(label "$engine")"
done

echo
if [ "$fail" -eq 0 ]; then
  echo "All ${total} databases are seeded and the datafix_user role is created (${parts})."
  exit 0
else
  echo "One or more databases could not be seeded. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md."
  exit 1
fi

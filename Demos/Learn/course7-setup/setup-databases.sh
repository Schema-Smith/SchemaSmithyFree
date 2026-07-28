#!/usr/bin/env bash
# Create the Course 7 tenant fleet on each sandbox engine, or on your own server's single
# activated engine (LEARN_SERVER): five EMPTY databases fleet_tenant_001..005. No schema is
# seeded -- the Module 1 deploy is what forges the Shop schema into each tenant. Re-running
# is safe -- creation is idempotent. PASS is reported only after the five databases are
# confirmed to exist on an engine.
#
# Tenant creation goes through lab_confirm_db (rather than the per-engine seed files under
# seed/<engine>/01_create_tenant_databases.sql, which issue the same guarded CREATE DATABASE
# statements) so each tenant is stamped as lab-provisioned -- that stamp is what lets
# --reset safely drop and recreate the fleet on your own server without ever touching a
# same-named database it didn't create.
#
# --reset drops and recreates the five tenant databases empty. Only databases the labs
# created are ever dropped -- see lab_remove_db.
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

tenants=(fleet_tenant_001 fleet_tenant_002 fleet_tenant_003 fleet_tenant_004 fleet_tenant_005)

tenant_names() {
  local engine="$1" sql
  case "$engine" in
    sqlserver) sql="SELECT name FROM sys.databases WHERE name LIKE 'fleet[_]tenant[_]%'" ;;
    postgres)  sql="SELECT datname FROM pg_database WHERE datname LIKE 'fleet\_tenant\_%'" ;;
    *)         sql="SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'fleet\_tenant\_%'" ;;
  esac
  lab_sql "$engine" "$(lab_admin_db "$engine")" "$sql" | tr -d '\r'
}

seed_engine() {
  local engine="$1" db removed err found missing extra note
  printf '%-12s ' "$(label "$engine")"

  if [ "$reset" -eq 1 ]; then
    # Drop the whole fleet, not just 001-005: Module 3 onboards fleet_tenant_006, and a
    # reset that left it behind wouldn't be the clean slate it promises.
    for db in $(tenant_names "$engine"); do
      removed="$(lab_remove_db "$engine" "$db" 2>/dev/null)"
      if [ "$removed" = "refused" ]; then
        echo "FAIL"
        echo "    '$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
        fail=1
        return 1
      fi
    done
  fi

  for db in "${tenants[@]}"; do
    err="$(lab_confirm_db "$engine" "$db" 2>&1 1>/dev/null)"
    if [ $? -ne 0 ]; then
      echo "FAIL"
      echo "$err" | sed 's/^/    /'
      fail=1
      return 1
    fi
  done

  # Confirm the five the course needs. Extra tenants are course progress, not a fault:
  # Module 3 onboards fleet_tenant_006 on purpose, so a strict count would report FAIL
  # for having followed the course.
  found="$(tenant_names "$engine")"
  missing=''
  for db in "${tenants[@]}"; do
    echo "$found" | grep -qx "$db" || missing="${missing:+$missing, }$db"
  done
  if [ -n "$missing" ]; then
    echo "FAIL"
    echo "    missing tenant database(s): $missing"
    fail=1
    return 1
  fi
  extra=''
  for db in $found; do
    case " ${tenants[*]} " in
      *" $db "*) ;;
      *) extra="${extra:+$extra, }$db" ;;
    esac
  done

  if [ "$reset" -eq 1 ]; then note="PASS (reset, 5 tenant databases)"; else note="PASS (5 tenant databases)"; fi
  [ -n "$extra" ] && note="$note + $extra from a later module"
  echo "$note"
}

engines="$(lab_engines)" || exit 1
n_engines=0
for engine in $engines; do
  seed_engine "$engine"
  n_engines=$((n_engines + 1))
done

echo
if [ "$fail" -eq 0 ]; then
  total=$((5 * n_engines))
  parts=""
  for engine in $engines; do
    parts="${parts}${parts:+, }5 $(label "$engine")"
  done
  echo "All ${total} tenant databases created (${parts}) — empty, ready for Module 1."
  exit 0
else
  echo "One or more engines could not be set up. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md."
  exit 1
fi

#!/usr/bin/env bash
# Create the Course 7 Module 7 fleet registries on each sandbox engine, or on your own
# server's single activated engine (LEARN_SERVER): three registry databases per engine
# (Dev/Prod/Empty), each holding a seeded Tenants table that Module 7's
# DatabaseIdentificationScript reads to discover which tenant databases to deploy into.
# Dev and Prod both list fleet_tenant_001..005 -- Dev has 005 inactive, Prod has it active.
# Empty gets the Tenants table with zero rows, to exercise a registry with no tenants.
# Re-running is safe -- creation and seeding are idempotent. PASS is reported only after
# the three registries' Tenants row counts are confirmed (Dev=5, Prod=5, Empty=0).
#
# Registry creation goes through lab_confirm_db so each registry is stamped as
# lab-provisioned -- that stamp is what lets --reset safely drop and recreate the
# registries on your own server without ever touching a same-named database it didn't
# create.
#
# --reset drops and recreates the three registry databases (re-seeded). Only databases the
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

kinds=(dev prod empty)

reg_label() {
  case "$1" in
    dev)   printf 'Dev' ;;
    prod)  printf 'Prod' ;;
    empty) printf 'Empty' ;;
  esac
}

registry_db() {
  local engine="$1" kind="$2"
  case "$engine" in
    sqlserver)
      case "$kind" in
        dev)   printf 'FleetRegistry_Dev' ;;
        prod)  printf 'FleetRegistry_Prod' ;;
        empty) printf 'FleetRegistry_Empty' ;;
      esac
      ;;
    *)
      case "$kind" in
        dev)   printf 'fleetregistry_dev' ;;
        prod)  printf 'fleetregistry_prod' ;;
        empty) printf 'fleetregistry_empty' ;;
      esac
      ;;
  esac
}

table_ref() {
  case "$1" in
    sqlserver) printf 'dbo.Tenants' ;;
    postgres)  printf 'public.tenants' ;;
    *)         printf 'Tenants' ;;
  esac
}

count_sql() {
  printf 'SELECT COUNT(*) FROM %s' "$(table_ref "$1")"
}

expected_count() {
  case "$1" in
    dev|prod) printf '5' ;;
    empty)    printf '0' ;;
  esac
}

# Builds the CREATE TABLE + seed statement for one engine/kind. Dev and Prod differ only in
# fleet_tenant_005's Active flag; Empty gets the table with no seed at all.
ddl_sql() {
  local engine="$1" kind="$2" five_active='1' five_bool='true' sql
  [ "$kind" = "dev" ] && five_active='0'

  case "$engine" in
    sqlserver)
      sql="IF OBJECT_ID('dbo.Tenants') IS NULL CREATE TABLE dbo.Tenants (DbName sysname NOT NULL PRIMARY KEY, Active bit NOT NULL);"
      if [ "$kind" != "empty" ]; then
        sql="$sql MERGE dbo.Tenants AS t USING (VALUES ('fleet_tenant_001',1),('fleet_tenant_002',1),('fleet_tenant_003',1),('fleet_tenant_004',1),('fleet_tenant_005',$five_active)) AS s(DbName,Active) ON t.DbName=s.DbName WHEN NOT MATCHED THEN INSERT(DbName,Active) VALUES(s.DbName,s.Active) WHEN MATCHED THEN UPDATE SET Active=s.Active;"
      fi
      ;;
    postgres)
      sql="CREATE TABLE IF NOT EXISTS public.tenants (dbname text PRIMARY KEY, active boolean NOT NULL);"
      if [ "$kind" != "empty" ]; then
        [ "$kind" = "dev" ] && five_bool='false'
        sql="$sql INSERT INTO public.tenants(dbname,active) VALUES ('fleet_tenant_001',true),('fleet_tenant_002',true),('fleet_tenant_003',true),('fleet_tenant_004',true),('fleet_tenant_005',$five_bool) ON CONFLICT (dbname) DO UPDATE SET active=EXCLUDED.active;"
      fi
      ;;
    *)
      sql="CREATE TABLE IF NOT EXISTS Tenants (DbName varchar(128) NOT NULL PRIMARY KEY, Active tinyint NOT NULL);"
      if [ "$kind" != "empty" ]; then
        sql="$sql INSERT INTO Tenants(DbName,Active) VALUES ('fleet_tenant_001',1),('fleet_tenant_002',1),('fleet_tenant_003',1),('fleet_tenant_004',1),('fleet_tenant_005',$five_active) ON DUPLICATE KEY UPDATE Active=VALUES(Active);"
      fi
      ;;
  esac
  printf '%s' "$sql"
}

seed_engine() {
  local engine="$1" kind db removed err sql countsql found expected note
  printf '%-12s ' "$(label "$engine")"

  if [ "$reset" -eq 1 ]; then
    for kind in "${kinds[@]}"; do
      db="$(registry_db "$engine" "$kind")"
      removed="$(lab_remove_db "$engine" "$db" 2>/dev/null)"
      if [ "$removed" = "refused" ]; then
        echo "FAIL"
        echo "    '$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
        fail=1
        return 1
      fi
    done
  fi

  for kind in "${kinds[@]}"; do
    db="$(registry_db "$engine" "$kind")"
    err="$(lab_confirm_db "$engine" "$db" 2>&1 1>/dev/null)"
    if [ $? -ne 0 ]; then
      echo "FAIL"
      echo "$err" | sed 's/^/    /'
      fail=1
      return 1
    fi

    sql="$(ddl_sql "$engine" "$kind")"
    err="$(lab_sql "$engine" "$db" "$sql" 2>&1 1>/dev/null)"
    if [ $? -ne 0 ]; then
      echo "FAIL"
      echo "$err" | sed 's/^/    /'
      fail=1
      return 1
    fi
  done

  for kind in "${kinds[@]}"; do
    db="$(registry_db "$engine" "$kind")"
    expected="$(expected_count "$kind")"
    countsql="$(count_sql "$engine")"
    found="$(lab_sql "$engine" "$db" "$countsql")"
    found="$(printf '%s' "$found" | tr -d '\r')"
    if [ "$found" != "$expected" ]; then
      echo "FAIL"
      echo "    $db ($(reg_label "$kind")): expected $expected row(s) in $(table_ref "$engine"), found '$found'."
      fail=1
      return 1
    fi
  done

  if [ "$reset" -eq 1 ]; then note="PASS (reset, 3 registries: Dev=5, Prod=5, Empty=0)"; else note="PASS (3 registries: Dev=5, Prod=5, Empty=0)"; fi
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
  total=$((3 * n_engines))
  parts=""
  for engine in $engines; do
    parts="${parts}${parts:+, }3 $(label "$engine")"
  done
  echo "All ${total} registry databases created (${parts}) — seeded (Dev=5, Prod=5, Empty=0 Tenants rows), ready for Module 7."
  exit 0
else
  echo "One or more engines could not be set up. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md."
  exit 1
fi

#!/usr/bin/env bash
# Create the Course 9 Northwind Commerce service databases on each sandbox engine, or on
# your own server's single activated engine (LEARN_SERVER): orders on SQL Server, catalog
# on PostgreSQL, sessions on MySQL. No schema is seeded -- each course module deploys its
# own native package into these databases. Re-running is safe -- lab_confirm_db only
# creates what's missing. PASS is reported only after the database is confirmed to exist
# (a create that silently fails reports FAIL, never a false PASS).
#
# --reset drops and recreates them empty. Use it any time you want a clean slate -- e.g.
# after a module's deploy fails partway and leaves a service in a state a later module
# doesn't expect. Only databases the labs created are ever dropped -- see lab_remove_db.
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/../lab-sql.sh"

reset=0
[ "${1:-}" = "--reset" ] && reset=1

fail=0
total=0
ready_dbs=""

label() {
  case "$1" in
    sqlserver) printf 'Orders (SQL Server)' ;;
    postgres)  printf 'Catalog (PostgreSQL)' ;;
    mysql)     printf 'Sessions (MySQL)' ;;
  esac
}

db_for() {
  case "$1" in
    sqlserver) printf 'orders' ;;
    postgres)  printf 'catalog' ;;
    mysql)     printf 'sessions' ;;
  esac
}

is_service_engine() {
  case "$1" in
    sqlserver|postgres|mysql) return 0 ;;
    *) return 1 ;;
  esac
}

engines="$(lab_engines)" || exit 1
for engine in $engines; do
  is_service_engine "$engine" || continue
  db="$(db_for "$engine")"
  printf '%-30s ' "$(label "$engine")"
  total=$((total + 1))
  rc=0
  err=''
  if [ "$reset" -eq 1 ]; then
    removed="$(lab_remove_db "$engine" "$db" 2>/dev/null)"
    if [ "$removed" = "refused" ]; then
      err="    '$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
      rc=1
    fi
  fi
  if [ "$rc" -eq 0 ]; then
    err="$(lab_confirm_db "$engine" "$db" 2>&1 1>/dev/null)"
    rc=$?
  fi
  if [ "$rc" -eq 0 ]; then
    if [ "$reset" -eq 1 ]; then echo "PASS (reset)"; else echo "PASS (service database ready)"; fi
    ready_dbs="${ready_dbs:+$ready_dbs, }$db"
  else
    echo "FAIL"
    echo "$err" | sed 's/^/    /'
    fail=1
  fi
done

echo
if [ "$total" -eq 0 ]; then
  echo "Course 9 doesn't use ${LEARN_ENGINE:-your engine} -- it needs SQL Server, PostgreSQL, and MySQL together. Point use-my-server at one of those instead, or use the sandbox for this course."
  exit 1
elif [ "$fail" -eq 0 ]; then
  if [ "$total" -eq 1 ]; then
    echo "The $ready_dbs service database is ready."
  else
    echo "All $total service databases are ready -- $ready_dbs."
  fi
  if lab_own_server && [ "$total" -lt 3 ]; then
    echo
    echo "Course 9 runs all three services together, so it needs SQL Server, PostgreSQL, and MySQL."
    echo "You have $total of the three. If you run the other engines too, re-source use-my-server for"
    echo "each one and run this script again -- otherwise use the sandbox for this course."
  fi
  exit 0
else
  echo "One or more engines could not be set up. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md."
  exit 1
fi

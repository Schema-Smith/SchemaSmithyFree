#!/usr/bin/env bash
# Create the Course 4 Recipe 10 replication topology databases -- SQL Server only, since
# this recipe's mechanism (a cross-database EXEC from an After Script) is SQL Server
# specific. Two databases: Shop_Primary (the publisher -- Customers, Orders, Inventory) and
# Shop_Replica (the subscriber -- gets only the tables whose Extensions.ReplicationEnabled
# is true, deployed into it by Shop_Primary's After Script). Re-running is safe -- creation
# is idempotent. PASS is reported only after both databases are confirmed to exist.
#
# --reset drops and recreates both databases empty. Only a database the labs created is
# ever dropped -- see lab_remove_db.
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/../lab-sql.sh"

engine="sqlserver"
dbs=("Shop_Primary" "Shop_Replica")
reset=0
[ "${1:-}" = "--reset" ] && reset=1

fail=0

if [ "$reset" -eq 1 ]; then
  for db in "${dbs[@]}"; do
    removed="$(lab_remove_db "$engine" "$db" 2>/dev/null)"
    if [ "$removed" = "refused" ]; then
      echo "FAIL"
      echo "    '$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
      fail=1
      break
    fi
  done
fi

if [ "$fail" -eq 0 ]; then
  for db in "${dbs[@]}"; do
    err="$(lab_confirm_db "$engine" "$db" 2>&1 1>/dev/null)"
    if [ $? -ne 0 ]; then
      echo "FAIL"
      echo "$err" | sed 's/^/    /'
      fail=1
      break
    fi
  done
fi

echo
if [ "$fail" -eq 0 ]; then
  if [ "$reset" -eq 1 ]; then note="PASS (reset, Shop_Primary + Shop_Replica ready)"; else note="PASS (Shop_Primary + Shop_Replica ready)"; fi
  echo "$note"
  echo "Shop_Primary and Shop_Replica ready on SQL Server -- deploy the Package to get started."
  exit 0
else
  echo "Could not set up the SQL Server databases. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md."
  exit 1
fi

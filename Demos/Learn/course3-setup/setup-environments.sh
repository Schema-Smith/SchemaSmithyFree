#!/usr/bin/env bash
# Create the Course 3 target databases (dev / staging / prod) on each sandbox engine, or
# on your own server's single activated engine (LEARN_SERVER). Re-running is safe -- all
# DDL is idempotent. PASS is reported only after the database is confirmed to exist (a
# create that silently fails reports FAIL, never a false PASS).
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/../lab-sql.sh"

fail=0
total=0

label() {
  case "$1" in
    sqlserver) printf 'SQL Server' ;;
    postgres)  printf 'PostgreSQL' ;;
    mysql)     printf 'MySQL' ;;
    mariadb)   printf 'MariaDB' ;;
  esac
}

engines="$(lab_engines)" || exit 1
for engine in $engines; do
  echo "$(label "$engine")"
  for env in dev staging prod; do
    db="ordersservice_${env}"
    printf '  %-26s ' "$db"
    total=$((total + 1))
    err="$(lab_confirm_db "$engine" "$db" 2>&1 1>/dev/null)"
    rc=$?
    if [ "$rc" -eq 0 ]; then
      echo "PASS"
    else
      echo "FAIL"
      echo "$err" | sed 's/^/    /'
      fail=1
    fi
  done
done

echo
if [ "$fail" -eq 0 ]; then
  echo "All ${total} databases are ready."
  exit 0
else
  echo "One or more databases could not be created. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md."
  exit 1
fi

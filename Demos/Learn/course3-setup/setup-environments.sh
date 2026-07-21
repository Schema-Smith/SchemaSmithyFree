#!/usr/bin/env bash
# Create the Course 3 target databases (dev / staging / prod) on each sandbox engine.
# Re-running is safe — all DDL is idempotent. PASS is reported only after the database
# is confirmed to exist (a create that silently fails reports FAIL, never a false PASS).
set -u

fail=0

confirm_db() {
  local engine="$1" db="$2" out=""
  printf '  %-26s ' "$db"
  case "$engine" in
    sqlserver)
      # SQL Server's sqlcmd lives at an absolute container path; wrap in `bash -c`
      # so Git Bash on Windows doesn't rewrite /opt/... into a host path.
      docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q \"IF DB_ID('${db}') IS NULL CREATE DATABASE [${db}]\"" >/dev/null 2>&1
      out=$(docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -Q \"SELECT 'READY' WHERE DB_ID('${db}') IS NOT NULL\"" 2>/dev/null)
      ;;
    postgres)
      docker exec learn-postgres psql -U postgres -d postgres -c "CREATE DATABASE ${db}" >/dev/null 2>&1
      out=$(docker exec learn-postgres psql -U postgres -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='${db}'" 2>/dev/null)
      ;;
    mysql)
      docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS \`${db}\`" >/dev/null 2>&1
      out=$(docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='${db}'" 2>/dev/null)
      ;;
    mariadb)
      docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS \`${db}\`" >/dev/null 2>&1
      out=$(docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='${db}'" 2>/dev/null)
      ;;
  esac
  if echo "$out" | grep -qE 'READY|1'; then
    echo "PASS"
  else
    echo "FAIL"
    fail=1
  fi
}

echo "SQL Server"
for env in dev staging prod; do confirm_db sqlserver "ordersservice_${env}"; done

echo "PostgreSQL"
for env in dev staging prod; do confirm_db postgres "ordersservice_${env}"; done

echo "MySQL"
for env in dev staging prod; do confirm_db mysql "ordersservice_${env}"; done

echo "MariaDB"
for env in dev staging prod; do confirm_db mariadb "ordersservice_${env}"; done

echo
if [ "$fail" -eq 0 ]; then
  echo "All twelve databases are ready."
  exit 0
else
  echo "One or more databases could not be created. Is the sandbox up? See Demos/Learn/README.md."
  exit 1
fi

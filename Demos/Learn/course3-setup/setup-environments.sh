#!/usr/bin/env bash
# Create the Course 3 target databases (dev / staging / prod) on each sandbox engine.
# Re-running is safe — all DDL is idempotent.
set -u

fail=0

# ── helpers ────────────────────────────────────────────────────────────────────

check_db() {
  local label="$1"
  shift
  printf '  %-26s ' "$label"
  if "$@" >/dev/null 2>&1; then
    echo "PASS"
  else
    echo "FAIL"
    fail=1
  fi
}

# ── SQL Server ──────────────────────────────────────────────────────────────────

echo "SQL Server"
for env in dev staging prod; do
  db="ordersservice_${env}"
  check_db "$db" \
    docker exec learn-sqlserver bash -c \
      "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q \"IF DB_ID('${db}') IS NULL CREATE DATABASE ${db}\""
done

# ── PostgreSQL ──────────────────────────────────────────────────────────────────

echo "PostgreSQL"
for env in dev staging prod; do
  db="ordersservice_${env}"
  check_db "$db" \
    docker exec learn-postgres bash -c \
      "psql -U postgres -d postgres -tAc \"SELECT 1 FROM pg_database WHERE datname='${db}'\" | grep -q 1 || psql -U postgres -d postgres -c 'CREATE DATABASE ${db}'"
done

# ── MySQL ───────────────────────────────────────────────────────────────────────

echo "MySQL"
for env in dev staging prod; do
  db="ordersservice_${env}"
  check_db "$db" \
    docker exec learn-mysql \
      mysql -uroot -pLearn!Passw0rd \
        -e "CREATE DATABASE IF NOT EXISTS ${db}"
done

# ── result ──────────────────────────────────────────────────────────────────────

echo
if [ "$fail" -eq 0 ]; then
  echo "All nine databases are ready."
  exit 0
else
  echo "One or more databases could not be created. Is the sandbox up? See Demos/Learn/README.md."
  exit 1
fi

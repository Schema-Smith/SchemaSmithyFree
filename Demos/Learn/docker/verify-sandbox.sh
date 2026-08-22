#!/usr/bin/env bash
# Verify the SchemaSmith Learn sandbox: wait for each engine to report healthy,
# then run a trivial connection/query against it. Prints PASS/FAIL per engine.
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/../lab-sql.sh"
if lab_own_server; then
  echo "This script verifies the Docker sandbox, which you're not using - you're pointed at ${LEARN_SERVER}:${LEARN_PORT}."
  echo "Nothing to verify here. Run each course's setup script instead."
  exit 0
fi

fail=0

wait_healthy() {
  local name="$1" tries=90
  for ((i = 1; i <= tries; i++)); do
    local status
    status="$(docker inspect -f '{{.State.Health.Status}}' "$name" 2>/dev/null || echo missing)"
    [ "$status" = "healthy" ] && return 0
    [ "$status" = "missing" ] && return 2
    sleep 2
  done
  return 1
}

check() {
  local label="$1" container="$2"
  shift 2
  printf '%-12s ' "$label"
  case "$(wait_healthy "$container"; echo $?)" in
    *2) echo "FAIL (container '$container' not found — is the sandbox up?)"; fail=1; return ;;
    *1) echo "FAIL (never became healthy)"; fail=1; return ;;
  esac
  if "$@" >/dev/null 2>&1; then
    echo "PASS"
  else
    echo "FAIL (could not run a query)"
    fail=1
  fi
}

echo "Verifying the SchemaSmith Learn sandbox (this can take a minute on first start)..."
check "SQL Server" learn-sqlserver docker exec learn-sqlserver bash -c 'exec 3<>/dev/tcp/localhost/1433'
check "PostgreSQL" learn-postgres  docker exec learn-postgres  psql -U postgres -d learn -tAc 'SELECT 1'
check "MySQL"      learn-mysql     docker exec learn-mysql      mysql -uroot -pLearn!Passw0rd -N -e 'SELECT 1'
# MariaDB 10.2-10.4 images ship only the 'mysql' client; later ones add 'mariadb'.
check "MariaDB"    learn-mariadb   docker exec learn-mariadb    sh -c 'command -v mariadb >/dev/null 2>&1 && CLI=mariadb || CLI=mysql; $CLI -uroot -pLearn!Passw0rd -N -e "SELECT 1"'

echo
if [ "$fail" -eq 0 ]; then
  echo "All four engines are ready."
  exit 0
else
  echo "One or more engines failed. Try again in a minute, or check 'docker compose logs'."
  exit 1
fi

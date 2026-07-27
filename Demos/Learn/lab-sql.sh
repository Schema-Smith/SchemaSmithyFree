#!/usr/bin/env bash
# Shared connection helper for the Learn labs.
#
# Sandbox (default): runs each engine's client inside its container, exactly as the labs
# always have. Own server (LEARN_SERVER set by use-my-server.sh): runs the engine's
# client on your machine against your endpoint. The output is identical either way, so
# callers never care which path ran.
#
# Source it as a library:  . ../lab-sql.sh
# Or call it as a command: ../lab-sql.sh sqlserver ordersservice_prod "SELECT 1"

lab_own_server() { [ -n "${LEARN_SERVER:-}" ]; }

lab_engines() {
  if ! lab_own_server; then printf 'sqlserver\npostgres\nmysql\nmariadb\n'; return 0; fi
  if [ -z "${LEARN_ENGINE:-}" ]; then
    echo "LAB-SQL: LEARN_SERVER is set but LEARN_ENGINE is not. Re-run use-my-server.sh." >&2
    return 1
  fi
  printf '%s\n' "$LEARN_ENGINE"
}

lab_endpoint_label() {
  if lab_own_server; then echo "${LEARN_SERVER}:${LEARN_PORT}"; else echo "learn-$1 (container)"; fi
}

lab_client() {
  local client
  case "$1" in
    sqlserver) client=sqlcmd ;;
    postgres)  client=psql ;;
    mysql)     client=mysql ;;
    mariadb)   client=mariadb ;;
  esac
  if ! command -v "$client" >/dev/null 2>&1; then
    echo "LAB-SQL: '$client' is required to reach your own $1 server but is not on PATH. Install the engine's command-line client (see docs/end-user/guide/use-your-own-server.md), re-open your shell, and verify with '$client --version'." >&2
    return 1
  fi
  printf '%s' "$client"
}

lab_container() {
  case "$1" in
    sqlserver) printf 'learn-sqlserver' ;;
    postgres)  printf 'learn-postgres' ;;
    mysql)     printf 'learn-mysql' ;;
    mariadb)   printf 'learn-mariadb' ;;
  esac
}

# Trim leading and trailing whitespace so output matches the PowerShell twin exactly.
lab_trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

lab_sql() {
  local engine="$1" db="$2" sql="$3" out rc client container
  if lab_own_server; then
    client="$(lab_client "$engine")" || return 1
    case "$engine" in
      sqlserver) out=$(sqlcmd -S "${LEARN_SERVER},${LEARN_PORT}" -U "$LEARN_USER" -P "$LEARN_PASSWORD" \
                         -C -b -d "$db" -h -1 -W -Q "SET NOCOUNT ON; $sql" 2>&1); rc=$? ;;
      postgres)  out=$(PGPASSWORD="$LEARN_PASSWORD" psql -h "$LEARN_SERVER" -p "$LEARN_PORT" -U "$LEARN_USER" \
                         -d "$db" -w -v ON_ERROR_STOP=1 --no-psqlrc -tAc "$sql" 2>&1); rc=$? ;;
      *)         out=$(MYSQL_PWD="$LEARN_PASSWORD" "$client" -h "$LEARN_SERVER" -P "$LEARN_PORT" \
                         -u "$LEARN_USER" -N -s -D "$db" -e "$sql" 2>&1); rc=$? ;;
    esac
  else
    container="$(lab_container "$engine")"
    case "$engine" in
      # MSYS_NO_PATHCONV keeps Git Bash from rewriting the container's /opt/... path;
      # it's harmless on Linux/macOS.
      sqlserver) out=$(MSYS_NO_PATHCONV=1 docker exec "$container" /opt/mssql-tools18/bin/sqlcmd \
                         -S localhost -U sa -P 'Learn!Passw0rd' -C -b -d "$db" -h -1 -W \
                         -Q "SET NOCOUNT ON; $sql" 2>&1); rc=$? ;;
      postgres)  out=$(docker exec "$container" psql -U postgres -d "$db" -v ON_ERROR_STOP=1 -tAc "$sql" 2>&1); rc=$? ;;
      mysql)     out=$(docker exec -e 'MYSQL_PWD=Learn!Passw0rd' "$container" mysql -uroot -N -s -D "$db" -e "$sql" 2>&1); rc=$? ;;
      mariadb)   out=$(docker exec -e 'MYSQL_PWD=Learn!Passw0rd' "$container" mariadb -uroot -N -s -D "$db" -e "$sql" 2>&1); rc=$? ;;
    esac
  fi
  if [ "$rc" -ne 0 ]; then
    echo "LAB-SQL: could not reach $engine at $(lab_endpoint_label "$engine") [database '$db'] -- $(lab_trim "$out")" >&2
    return 1
  fi
  lab_trim "$out"
}

# Called as a command rather than sourced.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  if [ "$#" -lt 3 ]; then
    echo "usage: lab-sql.sh <engine> <database> <sql>" >&2
    exit 2
  fi
  lab_sql "$1" "$2" "$3"
fi

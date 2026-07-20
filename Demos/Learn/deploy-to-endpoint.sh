#!/usr/bin/env bash
set -euo pipefail

# The no-Docker sibling of Demos/Learn/docker: creates (or cleanly resets) the empty,
# helper-owned `learn` sandbox database on a server you already run, so a learner can do
# the labs without Docker. It stamps the database it creates and only ever drops a stamped
# `learn` -- an existing `learn` it didn't create is refused, never touched.

ENGINE=""; SERVER=""; PORT=""; USER_=""; PASSWORD=""; FORCE=0
while [ $# -gt 0 ]; do case "$1" in
  --engine) ENGINE="$2"; shift 2;; --server) SERVER="$2"; shift 2;;
  --port) PORT="$2"; shift 2;; --user) USER_="$2"; shift 2;;
  --password) PASSWORD="$2"; shift 2;; --force) FORCE=1; shift;;
  *) echo "unknown arg: $1" >&2; exit 64;; esac; done

case "$ENGINE" in
  sqlserver) CLIENT=sqlcmd;  DEFPORT=1433;;
  postgres)  CLIENT=psql;    DEFPORT=5432;;
  mysql)     CLIENT=mysql;   DEFPORT=3306;;
  mariadb)   CLIENT=mariadb; DEFPORT=3306;;
  *) echo "--engine must be one of: sqlserver | postgres | mysql | mariadb" >&2; exit 64;;
esac
[ -n "$SERVER" ] || { echo "--server is required" >&2; exit 64; }
[ -n "$PORT" ] || PORT="$DEFPORT"

command -v "$CLIENT" >/dev/null 2>&1 || {
  echo "$CLIENT is required for --engine $ENGINE but was not found on PATH." >&2
  echo "Install the engine's command-line client (see docs/end-user/guide/use-your-own-server.md), then verify with '$CLIENT --version'." >&2
  exit 1; }
if [ "$ENGINE" != sqlserver ] && [ -z "$USER_" ]; then
  echo "--user and --password are required for --engine $ENGINE (only SQL Server supports Windows Authentication)." >&2
  exit 1
fi

# Run a SQL batch (read from stdin) against the target; returns stdout.
sql() {
  case "$ENGINE" in
    sqlserver)
      local auth; if [ -n "$USER_" ]; then auth=(-U "$USER_" -P "$PASSWORD"); else auth=(-E); fi
      sqlcmd -S "$SERVER,$PORT" "${auth[@]}" -C -b -h -1 -W ;;
    postgres)
      PGPASSWORD="$PASSWORD" psql -h "$SERVER" -p "$PORT" -U "$USER_" -d postgres -w \
        -v ON_ERROR_STOP=1 --no-psqlrc -t -A ;;
    *)
      MYSQL_PWD="$PASSWORD" "$CLIENT" -h "$SERVER" -P "$PORT" -u "$USER_" -N -s ;;
  esac
}

check_sql() { case "$ENGINE" in
  sqlserver) cat <<'EOF'
SET NOCOUNT ON;
IF DB_ID('learn') IS NULL BEGIN PRINT 'STAMP_RESULT:absent'; RETURN; END;
DECLARE @s BIT;
EXEC sp_executesql N'SELECT @s = CASE WHEN EXISTS (SELECT 1 FROM [learn].sys.extended_properties WHERE class = 0 AND name = ''SchemaSmith_DemoProvisioned'') THEN 1 ELSE 0 END', N'@s BIT OUTPUT', @s = @s OUTPUT;
PRINT 'STAMP_RESULT:' + CASE WHEN @s = 1 THEN 'stamped' ELSE 'unstamped' END;
EOF
;; postgres) cat <<'EOF'
SELECT CASE
  WHEN NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'learn') THEN 'STAMP_RESULT:absent'
  WHEN COALESCE((SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname = 'learn'), '') = 'SchemaSmith_DemoProvisioned' THEN 'STAMP_RESULT:stamped'
  ELSE 'STAMP_RESULT:unstamped' END;
EOF
;; *) cat <<'EOF'
SELECT CONCAT('STAMP_RESULT:', CASE
  WHEN NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'learn') THEN 'absent'
  WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'learn' AND table_name = 'SchemaSmith_DemoProvisioned') THEN 'stamped'
  ELSE 'unstamped' END);
EOF
;; esac; }

create_sql() { case "$ENGINE" in
  sqlserver) cat <<'EOF'
CREATE DATABASE [learn];
EXEC [learn].sys.sp_addextendedproperty @name = N'SchemaSmith_DemoProvisioned', @value = N'1';
EOF
;; postgres) cat <<'EOF'
CREATE DATABASE "learn";
COMMENT ON DATABASE "learn" IS 'SchemaSmith_DemoProvisioned';
EOF
;; *) cat <<'EOF'
CREATE DATABASE `learn` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE TABLE `learn`.`SchemaSmith_DemoProvisioned` (marker TINYINT NOT NULL);
EOF
;; esac; }

drop_sql() { case "$ENGINE" in
  sqlserver) cat <<'EOF'
ALTER DATABASE [learn] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [learn];
EOF
;; postgres) cat <<'EOF'
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'learn' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS "learn";
EOF
;; *) cat <<'EOF'
DROP DATABASE IF EXISTS `learn`;
EOF
;; esac; }

RES="$(check_sql | sql)"
case "$RES" in
  *STAMP_RESULT:unstamped*)
    echo "A database named 'learn' already exists on $SERVER but was NOT created by this helper." >&2
    echo "Refusing to drop a database the helper didn't create. If that 'learn' is yours, move or" >&2
    echo "rename it first; the helper always manages a database literally named 'learn'." >&2
    exit 2 ;;
esac
EXISTS=0; case "$RES" in *STAMP_RESULT:stamped*) EXISTS=1;; esac
if [ "$EXISTS" -eq 1 ] && [ "$FORCE" -ne 1 ]; then
  echo "The 'learn' sandbox database on $SERVER WILL BE DROPPED and recreated empty."
  read -r -p "Type 'yes' to continue: " ans; [ "$ans" = yes ] || { echo Aborted; exit 0; }
fi
[ "$EXISTS" -eq 1 ] && drop_sql | sql >/dev/null
create_sql | sql >/dev/null
echo "Done. Empty 'learn' sandbox ready on $SERVER ($ENGINE). Point your connect.settings.json at it and kindle the forge."

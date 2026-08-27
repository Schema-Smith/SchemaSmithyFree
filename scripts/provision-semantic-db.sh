#!/bin/sh
# Provision SQL Server's Semantic Language Statistics Database.
#
# STATISTICAL_SEMANTICS on a full-text index requires this database to be attached AND registered;
# without it CREATE FULLTEXT INDEX fails outright, so the feature cannot be tested at all. It is not
# something we can author -- it carries Microsoft's language models -- but the mssql-server-fts package
# ships it as a backup at /opt/mssql/misc/semanticsdb.bak, so any container with full-text installed can
# restore it locally. No download, no licence question, no Windows MSI.
#
# Idempotent: restores only when the database is absent, registers only when nothing is registered.
#
#   ./scripts/provision-semantic-db.sh <container> <user> <password> [port]
set -eu

CONTAINER="${1:?container name required}"
DB_USER="${2:?user required}"
DB_PASSWORD="${3:?password required}"
DB_PORT="${4:-1433}"

# The tools path moved between image generations; take whichever exists.
# Git Bash rewrites any argument that looks like an absolute path into a Windows one, which turns
# /opt/... into C:/Program Files/Git/opt/... before docker ever sees it. Disable that for this script;
# every path here belongs to the container, never to the host.
MSYS_NO_PATHCONV=1
MSYS2_ARG_CONV_EXCL="*"
export MSYS_NO_PATHCONV MSYS2_ARG_CONV_EXCL

SQLCMD="$(docker exec "$CONTAINER" sh -c 'ls /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd 2>/dev/null | head -1')"
if [ -z "$SQLCMD" ]; then
  echo "No sqlcmd found in $CONTAINER" >&2
  exit 1
fi

if ! docker exec "$CONTAINER" sh -c "test -f /opt/mssql/misc/semanticsdb.bak"; then
  echo "semanticsdb.bak not present -- is mssql-server-fts installed in $CONTAINER?" >&2
  exit 1
fi

docker exec "$CONTAINER" "$SQLCMD" -S "localhost,$DB_PORT" -U "$DB_USER" -P "$DB_PASSWORD" -C -b -Q "
IF DB_ID('semanticsdb') IS NULL
  RESTORE DATABASE semanticsdb FROM DISK = '/opt/mssql/misc/semanticsdb.bak'
    WITH MOVE 'semanticsdb' TO '/var/opt/mssql/data/semanticsdb.mdf',
         MOVE 'semanticsdb_log' TO '/var/opt/mssql/data/semanticsdb_log.ldf', RECOVERY;
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_semantic_language_statistics_database)
  EXEC sp_fulltext_semantic_register_language_statistics_db @dbname = N'semanticsdb';
"

docker exec "$CONTAINER" "$SQLCMD" -S "localhost,$DB_PORT" -U "$DB_USER" -P "$DB_PASSWORD" -C -h -1 -W -Q \
  "SELECT CONCAT('registered semantic databases: ', (SELECT COUNT(*) FROM sys.fulltext_semantic_language_statistics_database))"

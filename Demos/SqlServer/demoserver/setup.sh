# Wait for the port to open, then initialize the demo databases.
#
# On first boot the stock SQL Server image ships engine binaries newer than its
# templatedata system databases, so it spends minutes upgrading master/model/msdb
# before authentication works. During that window the port is already listening
# but sa logins fail transiently ("error evaluating the password"). A single-shot
# init lands in that window and the whole demo never comes up. So retry until the
# engine is genuinely ready, and treat an already-initialized server as success
# (the init renames sa -> $MSSQL_SA_USERNAME, so a restart can't reconnect as sa).
./wait-for-it.sh localhost:1433 --timeout=0 --strict -- sleep 5s

already_initialized() {
  /opt/mssql-tools18/bin/sqlcmd -C -U "$MSSQL_SA_USERNAME" -P "$MSSQL_SA_PASSWORD" -Q "USE TestSecondary" -b >/dev/null 2>&1
}

for attempt in $(seq 1 60); do
  if already_initialized; then
    echo "setup: demo databases already initialized"
    exit 0
  fi
  if /opt/mssql-tools18/bin/sqlcmd -C -i InitializeDatabase.sql -U sa -P "$MSSQL_SA_PASSWORD" -v MSSQL_SA_USERNAME="$MSSQL_SA_USERNAME" -b; then
    echo "setup: demo databases initialized"
    exit 0
  fi
  echo "setup: SQL Server not ready for initialization yet, retrying ($attempt/60)..."
  sleep 5
done

echo "setup: gave up waiting for SQL Server to become initializable" >&2
exit 1

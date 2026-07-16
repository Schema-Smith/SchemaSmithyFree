# Wait for the port to open, then initialize the demo databases.
#
# On first boot the stock SQL Server image ships templatedata system databases
# (version 927) older than its engine binaries (957), so a fresh instance runs a
# one-time, I/O-bound upgrade before authentication works -- seconds on a fast
# disk, but many minutes on a slow/constrained Docker backend, during which the
# port is already listening but sa logins fail ("error evaluating the password").
# A single-shot init lands in that window and the whole demo never comes up, so
# retry patiently (up to ~30 min) until the engine is genuinely ready. Treat an
# already-initialized server as success (the init renames sa -> $MSSQL_SA_USERNAME,
# so a restart can't reconnect as sa).
./wait-for-it.sh localhost:1433 --timeout=0 --strict -- sleep 5s

already_initialized() {
  /opt/mssql-tools18/bin/sqlcmd -C -U "$MSSQL_SA_USERNAME" -P "$MSSQL_SA_PASSWORD" -Q "USE TestSecondary" -b >/dev/null 2>&1
}

for attempt in $(seq 1 180); do
  if already_initialized; then
    echo "setup: demo databases already initialized"
    exit 0
  fi
  if /opt/mssql-tools18/bin/sqlcmd -C -i InitializeDatabase.sql -U sa -P "$MSSQL_SA_PASSWORD" -v MSSQL_SA_USERNAME="$MSSQL_SA_USERNAME" -b; then
    echo "setup: demo databases initialized"
    exit 0
  fi
  echo "setup: SQL Server still finishing first-boot startup, retrying ($attempt/180)..."
  sleep 10
done

echo "setup: gave up waiting for SQL Server to become initializable" >&2
exit 1

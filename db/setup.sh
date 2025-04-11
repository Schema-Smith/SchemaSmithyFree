# Wait for SQL Server to be started and then run the sql script
./wait-for-it.sh localhost:1433 --timeout=0 --strict -- sleep 5s && \
/opt/mssql-tools/bin/sqlcmd -i InitializeDatabase.sql -U sa -P "$MSSQL_SA_PASSWORD" -v MSSQL_SA_USERNAME="$MSSQL_SA_USERNAME"

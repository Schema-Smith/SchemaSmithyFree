#start sql agent
/opt/mssql/bin/mssql-conf set sqlagent.enabled true

#start SQL Server, start the script to create the DB and import the data, start the app
/opt/mssql/bin/sqlservr & ./setup.sh & sleep infinity & wait
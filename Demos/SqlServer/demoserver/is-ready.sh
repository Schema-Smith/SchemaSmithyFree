#!/bin/bash
# Healthy only once setup.sh has finished: sa renamed to $MSSQL_SA_USERNAME and
# the demo databases created. Quoting the user guards against an empty variable
# silently falling back to integrated auth (the container's machine account).
/opt/mssql-tools18/bin/sqlcmd -C -U "$MSSQL_SA_USERNAME" -P "$MSSQL_SA_PASSWORD" -Q "use TestSecondary" -b

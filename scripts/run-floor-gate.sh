#!/usr/bin/env bash
# Paths below are repo-relative; this anchors the script so it runs from anywhere.
cd "$(dirname "$0")/.." || exit 1
# Engine-FLOOR gate. The standing gate (run-gate.sh) runs against the current-version containers, so a
# defect that only bites at a declared floor passes it and fails CI -- which is exactly what happened on
# PR #392 (MySQL 5.7: parenthesized DEFAULT, SRS_ID, comment escaping). Run this BEFORE opening a PR.
#
# The SQL Server floor is NOT here: it needs genuine old binaries, not a container -- run-genuine-sweep.sh.
# These are the learn-sandbox floor containers, reached as root because they carry no TestUser.
set -u
FILTER="${1:-}"
run() { # name port user pass settings-prefix
  echo "########## $1 ##########"
  env "SmithySettings_$5__Server=127.0.0.1" "SmithySettings_$5__Port=$2" \
      "SmithySettings_$5__User=$3" "SmithySettings_$5__Password=$4" \
    dotnet test "$6" --no-build ${FILTER:+--filter "$FILTER"} 2>&1 \
    | grep -E "^(Passed!|Failed!)|^  Failed "
}
for proj in Schema/Schema.IntegrationTests/Schema.IntegrationTests.csproj \
            SchemaQuench/SchemaQuench.IntegrationTests/SchemaQuench.IntegrationTests.csproj \
            SchemaTongs/SchemaTongs.IntegrationTests/SchemaTongs.IntegrationTests.csproj \
            DataTongs/DataTongs.IntegrationTests/DataTongs.IntegrationTests.csproj; do
  echo "===== $(basename "$proj" .csproj) ====="
  run "MySQL 5.7 (floor)"     13316 root 'Learn!Passw0rd' MySQL      "$proj"
  run "MariaDB 10.2 (floor)"  13317 root 'Learn!Passw0rd' MariaDB    "$proj"
  run "PostgreSQL 12 (floor)" 15433 postgres 'Learn!Passw0rd' PostgreSQL "$proj"
done

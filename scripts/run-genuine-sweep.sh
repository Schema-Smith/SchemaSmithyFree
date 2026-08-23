#!/usr/bin/env bash
# Paths below are repo-relative; this anchors the script so it runs from anywhere.
cd "$(dirname "$0")/.." || exit 1
# Genuine-binary version sweep (pre-PR gate). LOCAL-ONLY: needs the four on-demand old SQL Server
# instances (C:\temp\sqlserver-oldbinaries\start-oldsql.ps1). CI has no genuine old binary.
#
# TWO DIFFERENT WIRINGS -- do not merge them:
#  1. Schema.IntegrationTests/GenuineOldBinary reads SmithySettings_SqlServer__* , so it must be
#     pointed at each old instance in turn. The folder now holds TWO fixture families with opposite
#     version predicates, so every instance both passes and skips some -- read the counts, not the
#     presence of skips:
#       * OldBinaryXml* prove the pre-2016 CREATE-time binding. They self-Ignore at 2016+, so they
#         run on 14330/14331/14332 and skip on 14333.
#       * Sql2016TemporalRetentionGateTests proves the 2016-only band where the JSON/STRING_AGG and
#         2017-catalog-column gates cross. It runs ONLY on major 13, so it skips on 14330/14331/14332
#         and runs on 14333. 14333 used to skip everything, which is exactly how two 2017-vs-2016
#         defects lived there unseen; an all-skip result on 14333 now means something is wrong.
#     Expected shape per instance: 4 passed / 4 skipped.
#  2. SchemaQuench's GenuineSql2008EmitGuardCert HARDCODES 127.0.0.1,14330 for its own work, but it
#     lives in the SqlServer namespace whose [SetUpFixture] provisions Always Encrypted keys. Point
#     the env vars at an old instance and that setup dies with "SQL Server instance in use does not
#     support column encryption" -- which looks like three product failures and is not. Run it with
#     the DEFAULT settings so the SetUpFixture reaches the modern container.
set -u
echo "===== 1. GenuineOldBinary, per instance ====="
for port in 14330 14331 14332 14333; do
  echo "--- instance $port"
  SmithySettings_SqlServer__Server=127.0.0.1 SmithySettings_SqlServer__Port=$port \
  SmithySettings_SqlServer__User=sa SmithySettings_SqlServer__Password='SchemaSmith!Old2026' \
  dotnet test Schema/Schema.IntegrationTests/Schema.IntegrationTests.csproj --no-build \
    --filter "FullyQualifiedName~GenuineOldBinary" 2>&1 | grep -E "^(Passed!|Failed!|No test)|^  (Failed|Skipped) "
done
echo "===== 2. 2008 emit-guard cert (default settings; fixture reaches 14330 itself) ====="
dotnet test SchemaQuench/SchemaQuench.IntegrationTests/SchemaQuench.IntegrationTests.csproj --no-build \
  --filter "FullyQualifiedName~GenuineSql2008EmitGuardCert" 2>&1 | grep -E "^(Passed!|Failed!)|^  Failed "

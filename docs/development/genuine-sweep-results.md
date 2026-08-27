# Genuine-binary sweep results

Appended by `scripts/run-genuine-sweep.sh`. CI cannot run this sweep -- no pre-2017 SQL
Server Linux image exists -- so this file is the standing evidence that it ran, and against what.

## 2026-08-26T17:26:17Z - commit 435c1d66

| Target | Result | Expected |
|---|---|---|
| SQL Server @ 14330 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 10 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14331 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 25 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14332 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 26 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14333 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 7 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |

## 2026-08-27T20:29:55Z - commit 50605d09

| Target | Result | Expected |
|---|---|---|
| SQL Server @ 14330 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 19 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14331 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 29 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14332 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 27 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14333 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 8 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |

## 2026-08-27T20:34:32Z - modern bands - commit 80b0d96d

| Target | Result | Expected |
|---|---|---|
| mcr.microsoft.com/mssql/server:2017-latest @ 14340 | NEVER READY | full SqlServer category |
| mcr.microsoft.com/mssql/server:2022-latest @ 14342 | NEVER READY | full SqlServer category |
| mcr.microsoft.com/mssql/server:2025-latest @ 14345 | NEVER READY | full SqlServer category |

> **The NEVER READY rows above are a tooling failure, not a product finding.** The readiness probe
> passed `$SQLCMD` to `docker exec` as a bare argv entry, and Git Bash rewrote it to
> `C:/Program Files/Git/opt/...` before docker saw it, so no band could ever report ready. Fixed in
> `scripts/run-modern-band-sweep.sh` (every container-side path now goes inside an `sh -c` string), and
> the script now exits non-zero instead of 0 when a band did not run. Real results follow below.

## 2026-08-27T21:14:36Z - commit aa26d3ad

| Target | Result | Expected |
|---|---|---|
| SQL Server @ 14330 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 10 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14331 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 22 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14332 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 24 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |
| SQL Server @ 14333 | Passed!  - Failed:     0, Passed:     4, Skipped:     4, Total:     8, Duration: 6 s - Schema.IntegrationTests.dll (net10.0) | 4 passed / 4 skipped |

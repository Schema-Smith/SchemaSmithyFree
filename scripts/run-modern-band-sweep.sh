#!/usr/bin/env bash
# Paths below are repo-relative; this anchors the script so it runs from anywhere.
cd "$(dirname "$0")/.." || exit 1
# Modern-band SQL Server sweep. CI runs exactly ONE SQL Server leg (2019), which is neither the floor
# nor the latest, so 2017, 2022 and 2025 are covered nowhere. This points the real integration suite at
# each of them in turn, using containers -- the bands where a Linux image exists.
#
# DELIBERATELY SEPARATE FROM run-genuine-sweep.sh, and not folded into it:
#   run-genuine-sweep.sh is the PRE-PR gate. It takes minutes and must keep taking minutes, because the
#   failure mode that item exists to prevent is somebody SKIPPING it -- two 2017-vs-2016 defects reached
#   the v2.5.0 cut exactly that way. Bolting three full integration runs onto it would make it an
#   hours-long gate, and an hours-long gate before every PR gets skipped, which trades a known gap for
#   a worse one.
#   This script is the PRE-RELEASE breadth pass instead: run once per release, where hours are affordable.
#
# NOT YET USABLE -- the containers below are under-provisioned. Its first real run (2026-08-27,
# after the Git Bash path bug below was fixed) reached all three bands and then failed 464/464
# SchemaQuench tests on every one of them, in 3 seconds, with:
#     Full-Text Search is not installed, or a full-text component cannot be loaded.  (error 7609)
# That is not a product finding. A stock mcr.microsoft.com/mssql/server image ships no full-text
# component, and the SchemaQuench SqlServer [SetUpFixture] requires one. Everywhere SQL Server
# tests actually pass, something installs it first:
#   * CI  -- an explicit "Install Full-Text Search on SQL Server" step (mssql-server-fts), then a
#            restart, then scripts/provision-semantic-db.sh for the STATISTICAL_SEMANTICS tests.
#   * local -- Demos/SqlServer/demoserver/demoserver.Dockerfile does the same at build time and
#            takes a BASE_IMAGE arg, which is exactly the per-band knob this script needs.
# The fix is to stop hand-rolling a container here and drive that demo image per band
# (MSSQL_IMAGE + MSSQL_PORT), which also deletes the bespoke sqlcmd probing below. One open
# question blocks a straight swap: that Dockerfile maps Ubuntu 20.04/22.04/24.04 -> 2019/2022/2025
# and hard-fails otherwise, so the 2017 band (Ubuntu 16.04, long EOL) needs either a 16.04 case
# that can still reach packages.microsoft.com, or an explicit decision to drop 2017 from the bands.
# Until that lands this script FAILS rather than reporting a pass -- see the exit guard at the end.
#
# Each band gets its own container and port so a leftover container from another band cannot silently
# answer for it -- the same failure shape as an all-skip reading as a pass.
set -u

RECORD="docs/development/genuine-sweep-results.md"
SWEEP_SHA="$(git rev-parse --short HEAD)"
SWEEP_STARTED="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
SWEEP_ROWS=""
PASSWORD='SchemaSmith!Band2026'

# port:image -- 2019 is absent on purpose, that is the leg CI already runs.
BANDS="14340:2017-latest 14342:2022-latest 14345:2025-latest"

cleanup() {
  for band in $BANDS; do
    docker rm -f "ss-band-${band%%:*}" >/dev/null 2>&1
  done
}
trap cleanup EXIT

for band in $BANDS; do
  port="${band%%:*}"
  image="mcr.microsoft.com/mssql/server:${band##*:}"
  name="ss-band-$port"
  echo "===== band $image on $port ====="

  docker rm -f "$name" >/dev/null 2>&1
  if ! docker run -d --name "$name" -p "$port:1433" \
       -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$PASSWORD" "$image" >/dev/null; then
    echo "  could not start $image -- recording as NOT RUN rather than skipping silently"
    SWEEP_ROWS="${SWEEP_ROWS}| $image @ $port | CONTAINER FAILED TO START | full SqlServer category |
"
    continue
  fi

  # sqlcmd moved between images: 2017 ships /opt/mssql-tools (no -C flag -- it predates the
  # mandatory-TLS client), while 2022+ ship /opt/mssql-tools18, which REQUIRES -C to trust the
  # self-signed cert. Hardcoding either one makes the other band report NEVER READY forever, a false
  # negative indistinguishable from a broken build. Detect rather than assume.
  # `docker exec "$name" test -x /opt/...` does NOT work from Git Bash, which is where this actually
  # gets run: MSYS rewrites a bare /opt/... argv entry into a Windows path before docker sees it, so the
  # test fails on 2022 and the script silently picks the 2017 tooling. Wrapping it in sh -c keeps the
  # path inside a string argument, which MSYS leaves alone. Verified both ways on both image generations.
  #
  # THE SAME REWRITE APPLIES TO EVERY container-side path below, not just this probe. It was
  # originally fixed here only, so $SQLCMD was still passed as a bare argv entry to the readiness
  # and version queries -- docker received "C:/Program Files/Git/opt/mssql-tools18/bin/sqlcmd",
  # every band reported NEVER READY, and the sweep exited 0 having run no tests at all. Keep every
  # container-side path inside an sh -c string.
  if docker exec "$name" sh -c 'test -x /opt/mssql-tools18/bin/sqlcmd' 2>/dev/null; then
    SQLCMD="/opt/mssql-tools18/bin/sqlcmd"; TLS="-C"
  else
    SQLCMD="/opt/mssql-tools/bin/sqlcmd"; TLS=""
  fi

  # Wait for the instance rather than sleeping a fixed amount: 2022 has markedly the slowest first boot
  # of the three (Demos/README.md), so any single sleep is either too short for it or wasted on the others.
  ready=0
  for _ in $(seq 1 60); do
    if docker exec "$name" sh -c \
         "$SQLCMD -S localhost -U sa -P '$PASSWORD' $TLS -Q 'SELECT 1'" >/dev/null 2>&1; then
      ready=1; break
    fi
    sleep 5
  done
  if [ "$ready" -ne 1 ]; then
    echo "  never became ready"
    SWEEP_ROWS="${SWEEP_ROWS}| $image @ $port | NEVER READY | full SqlServer category |
"
    docker rm -f "$name" >/dev/null 2>&1
    continue
  fi

  version="$(docker exec "$name" sh -c \
      "$SQLCMD -S localhost -U sa -P '$PASSWORD' $TLS -h -1 -W -Q \"SET NOCOUNT ON; SELECT CONVERT(varchar(20), SERVERPROPERTY('ProductVersion'))\"" \
      2>/dev/null | tr -d '\r' | head -1)"
  echo "  ready: $version"

  SmithySettings_SqlServer__Server=127.0.0.1 SmithySettings_SqlServer__Port=$port \
  SmithySettings_SqlServer__User=sa SmithySettings_SqlServer__Password="$PASSWORD" \
  dotnet test SchemaSmith.sln --filter "Category=SqlServer" 2>&1 \
    | tee "/tmp/ss-band-$port.log" | grep -E "^(Passed!|Failed!|No test)|^  Failed "

  # Every assembly, not just the first. `head -1` recorded whichever finished first, so a passing
  # Schema.IntegrationTests masked 464 failed SchemaQuench tests in the very same band -- and the row
  # then read "Passed!", which also starved the Failed! exit guard of anything to catch.
  # awk rather than bc: bc is not present in Git Bash, where this runs.
  band_totals="$(grep -E "^(Passed!|Failed!)" "/tmp/ss-band-$port.log" | awk '
    { for (i = 1; i <= NF; i++) {
        if ($i == "Failed:") f += $(i + 1);
        if ($i == "Passed:") p += $(i + 1);
      }
      n++ }
    END { printf "%d %d %d", f + 0, p + 0, n + 0 }')"
  band_failed="${band_totals%% *}"
  band_assemblies="${band_totals##* }"
  band_passed="$(printf "%s" "$band_totals" | cut -d" " -f2)"

  if [ -z "$band_totals" ] || [ "$band_assemblies" -eq 0 ]; then
    summary="NO RESULT"
  elif [ "$band_failed" -gt 0 ]; then
    summary="Failed! - $band_failed failed, $band_passed passed across $band_assemblies assemblies"
  else
    summary="Passed! - 0 failed, $band_passed passed across $band_assemblies assemblies"
  fi
  SWEEP_ROWS="${SWEEP_ROWS}| $image ($version) @ $port | ${summary:-NO RESULT} | full SqlServer category, 0 failed |
"
  docker rm -f "$name" >/dev/null 2>&1
done

# Appended to the same record the pre-PR sweep writes, so one file answers "what was this commit
# actually exercised against" across both passes.
mkdir -p "$(dirname "$RECORD")"
{
  echo ""
  echo "## $SWEEP_STARTED - modern bands - commit $SWEEP_SHA"
  echo ""
  echo "| Target | Result | Expected |"
  echo "|---|---|---|"
  printf "%s" "$SWEEP_ROWS"
} >> "$RECORD"
echo
echo "Recorded in $RECORD for commit $SWEEP_SHA -- commit it alongside the release it certifies."

# A band that never became ready, failed to start, or produced no result has NOT been swept, and the
# sweep must not report success for it. Exiting 0 on an all-NEVER-READY run is how a broken probe
# looked exactly like a clean pass -- the record said so plainly while the exit code said otherwise,
# and the exit code is what a caller (or a hook) actually reads.
if printf "%s" "$SWEEP_ROWS" | grep -qE "NEVER READY|CONTAINER FAILED TO START|NO RESULT|Failed!"; then
  echo "FAIL: at least one band did not run. The rows above are not a certification."
  exit 1
fi
if [ -z "$SWEEP_ROWS" ]; then
  echo "FAIL: no bands were attempted at all."
  exit 1
fi
echo "All bands ran."

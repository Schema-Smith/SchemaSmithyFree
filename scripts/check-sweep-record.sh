#!/usr/bin/env bash
# Paths below are repo-relative; this anchors the script so it runs from anywhere.
cd "$(dirname "$0")/.." || exit 1
# Asserts that the genuine-binary sweep record still covers the current tree.
#
# The sweep itself CANNOT run in CI -- no pre-2017 SQL Server Linux image exists -- so the only thing CI
# can enforce is that somebody ran it locally against a commit whose engine behaviour is still current.
# Human forgetting is the failure mode this guards: two 2017-vs-2016 defects reached the v2.5.0 cut
# because the sweep was skipped, not because it lacked coverage.
#
# "Still covers" is computed from the actual diff, never from a trigger path-filter: find the newest
# commit named in the record that is an ancestor of HEAD, then require that nothing which could change
# engine behaviour has changed since. Docs, demos and packaging cannot invalidate a sweep; engine
# scripts and product code obviously can.
set -u

RECORD="docs/development/genuine-sweep-results.md"

if [ ! -f "$RECORD" ]; then
  echo "FAIL: $RECORD does not exist -- the genuine-binary sweep has never been recorded."
  echo "Run scripts/run-genuine-sweep.sh and commit the record it appends."
  exit 1
fi

# Every recorded block carries "commit <short-sha>".
SHAS=$(grep -oE 'commit [0-9a-f]{7,40}' "$RECORD" | awk '{print $2}' | sort -u)
if [ -z "$SHAS" ]; then
  echo "FAIL: $RECORD names no commits -- it cannot certify anything."
  exit 1
fi

BEST=""
BEST_DEPTH=""
for sha in $SHAS; do
  git cat-file -e "${sha}^{commit}" 2>/dev/null || continue          # record may predate a rewrite
  git merge-base --is-ancestor "$sha" HEAD 2>/dev/null || continue    # or belong to another branch
  depth=$(git rev-list --count "${sha}..HEAD")
  if [ -z "$BEST_DEPTH" ] || [ "$depth" -lt "$BEST_DEPTH" ]; then
    BEST="$sha"; BEST_DEPTH="$depth"
  fi
done

if [ -z "$BEST" ]; then
  echo "FAIL: no commit named in $RECORD is an ancestor of HEAD."
  echo "The sweep has not been run on this line of work. Run scripts/run-genuine-sweep.sh."
  exit 1
fi

# What a sweep certifies is engine behaviour. Anything outside these paths cannot invalidate it.
INVALIDATING=$(git diff --name-only "$BEST" HEAD -- \
  'Schema/' 'SchemaQuench/' 'SchemaTongs/' 'DataTongs/' 'SchemaShears/' \
  ':(exclude)*/*.UnitTests/*' ':(exclude)*.md')

if [ -n "$INVALIDATING" ]; then
  echo "FAIL: the newest sweep record is $BEST ($BEST_DEPTH commits back), but product code has changed since:"
  echo "$INVALIDATING" | sed 's/^/    /' | head -40
  count=$(echo "$INVALIDATING" | wc -l)
  [ "$count" -gt 40 ] && echo "    ... and $((count - 40)) more"
  echo ""
  echo "Re-run scripts/run-genuine-sweep.sh and commit the record it appends."
  exit 1
fi

echo "PASS: sweep record $BEST covers HEAD ($BEST_DEPTH commits back, no engine or product changes since)."

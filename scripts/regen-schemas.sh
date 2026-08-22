#!/usr/bin/env bash
# Paths below are repo-relative; this anchors the script so it runs from anywhere.
cd "$(dirname "$0")/.." || exit 1
# Regenerate every committed .json-schemas directory (DoD #9).
# Required after any domain-model or SchemaGenerator change.
set -u
TONGS="SchemaTongs/bin/Debug/net10.0/SchemaTongs.dll"
[ -f "$TONGS" ] || { echo "build SchemaTongs first: dotnet build SchemaTongs/SchemaTongs.csproj"; exit 1; }
ok=0; fail=0
while IFS= read -r d; do
  pkg="$(dirname "$d")"
  [ -f "$pkg/Product.json" ] || continue
  # Skip ONLY the two fixtures whose .json-schemas state is itself the thing under test: StaleSchema
  # must STAY stale (it is what SS-STALE-001 detects) and PartialCommittedSchemas must keep ONE
  # type's schema missing (--WriteSchemasOnly writes every type and would fill the gap).
  # Every other Validate fixture MUST be regenerated, including the Misnamed* ones -- their defect
  # is a bad property in the package JSON, not a stale schema, and SS-STALE-001 short-circuits the
  # JSON-schema pass, so a stale schema there masks the very finding the test asserts.
  # StaleSchema must STAY stale -- its staleness is what SS-STALE-001 detects.
  case "$pkg" in
    */Fixtures/Validate/StaleSchema/*) continue;;
  esac
  if SmithySettings_Product__Path="$pkg" dotnet "$TONGS" --WriteSchemasOnly >/dev/null 2>&1; then
    # PartialCommittedSchemas IS regenerated -- a stale schema there fires SS-STALE-001, which
    # short-circuits the JSON-schema pass its test asserts on -- but --WriteSchemasOnly writes EVERY
    # type, filling the one-type gap that is the fixture's actual subject. Re-open the gap after.
    case "$pkg" in
      */Fixtures/Validate/PartialCommittedSchemas/*) rm -f "$pkg"/.json-schemas/tables.*.schema;;
    esac
    ok=$((ok+1))
  else
    fail=$((fail+1)); echo "FAILED: $pkg"
  fi
done < <(find . -name ".json-schemas" -type d -not -path "*/bin/*" -not -path "*/obj/*")
echo "regenerated: $ok   failed: $fail"

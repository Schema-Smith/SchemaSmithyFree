#!/usr/bin/env bash
# Course 3 · Module 2 — local CI/CD pipeline simulation (MariaDB)
#
# Simulates the merge -> staging -> prod flow the chapter describes. The COMMANDS
# this script runs ARE the contract; a CI YAML is just a wrapper around them.
#
# One built artifact (the solution Package) deploys to every environment. The ONLY
# thing that changes per environment is a handful of SmithySettings_* env vars:
#   SmithySettings_ScriptTokens__TargetDb   -> which database to converge
#   SmithySettings_WhatIfONLY               -> preview (PR check) vs. apply
#
# Re-runnable: every quench converges to the declared state and stops, so running
# the whole pipeline again is a clean no-op on already-converged databases.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/../../../lab-sql.sh"

SCHEMAQUENCH="${SCHEMAQUENCH:-schemaquench}"
STARTER="$(cd "$HERE/../../starter/mariadb" && pwd)"
SOLUTION="$(cd "$HERE/../../solution/mariadb" && pwd)"
SETTINGS="$SOLUTION/base.settings.json"          # connection + package defaults, shared by every env

index_state() {
  lab_sql mariadb "$1" "SELECT IFNULL((SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='$1' AND INDEX_NAME='IX_Customer_Email' LIMIT 1),'ABSENT')"
}

echo "=== Step 1: deploy the BASE (starter) to staging — establish current production state ==="
SmithySettings_SchemaPackagePath="$STARTER/Package" \
SmithySettings_ScriptTokens__TargetDb=ordersservice_staging \
SmithySettings_WhatIfONLY=false \
  "$SCHEMAQUENCH" --ConfigFile:"$SETTINGS"

echo
echo "=== Step 2: WhatIf the SOLUTION against staging — the PR check (no changes applied) ==="
SmithySettings_SchemaPackagePath="$SOLUTION/Package" \
SmithySettings_ScriptTokens__TargetDb=ordersservice_staging \
SmithySettings_WhatIfONLY=true \
  "$SCHEMAQUENCH" --ConfigFile:"$SETTINGS"

echo
echo "--- Prove WhatIf applied NOTHING: IX_Customer_Email on staging should read ABSENT ---"
state="$(index_state ordersservice_staging)"
echo "    staging IX_Customer_Email: $state"
if [ "$state" != "ABSENT" ]; then
  echo "    FAIL: WhatIf should not have created the index, but the catalog reports '$state'." >&2
  exit 1
fi

echo
echo "=== Step 3: WhatIf passed — deploy the SOLUTION to staging ==="
SmithySettings_SchemaPackagePath="$SOLUTION/Package" \
SmithySettings_ScriptTokens__TargetDb=ordersservice_staging \
SmithySettings_WhatIfONLY=false \
  "$SCHEMAQUENCH" --ConfigFile:"$SETTINGS"

echo
echo "=== Step 4: promote the SAME artifact to prod — only the env vars change ==="
SmithySettings_SchemaPackagePath="$SOLUTION/Package" \
SmithySettings_ScriptTokens__TargetDb=ordersservice_prod \
SmithySettings_WhatIfONLY=false \
  "$SCHEMAQUENCH" --ConfigFile:"$SETTINGS"

echo
echo "--- Both environments converged: IX_Customer_Email should now read present ---"
echo "    staging: $(index_state ordersservice_staging)"
echo "    prod:    $(index_state ordersservice_prod)"
echo
echo "Pipeline complete. Same artifact, staging then prod, gated by a WhatIf preview."

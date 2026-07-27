#!/usr/bin/env bash
# Self-test for lab-sql.sh. Requires the Learn sandbox to be up (docker compose up -d
# in Demos/Learn/docker) and sqlcmd on PATH for the own-server checks.
# Run: ./lab-sql.selftest.sh
set -u
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lab-sql.sh"

failed=0

assert_equal() {   # expected actual what
  if [ "$1" = "$2" ]; then echo "  PASS  $3"
  else echo "  FAIL  $3 (expected '$1', got '$2')"; failed=$((failed + 1)); fi
}
assert_fails() {   # match what -- command...
  local match="$1" what="$2"; shift 3
  local out rc
  out=$("$@" 2>&1); rc=$?
  if [ "$rc" -eq 0 ]; then echo "  FAIL  $what (no error raised)"; failed=$((failed + 1))
  elif echo "$out" | grep -q "$match"; then echo "  PASS  $what"
  else echo "  FAIL  $what (message was '$out')"; failed=$((failed + 1)); fi
}
clear_lab_env() { unset LEARN_SERVER LEARN_PORT LEARN_USER LEARN_PASSWORD LEARN_ENGINE || true; }
set_lab_env() {   # engine port
  export LEARN_SERVER=localhost LEARN_PORT="$2" LEARN_ENGINE="$1" LEARN_PASSWORD='Learn!Passw0rd'
  case "$1" in
    sqlserver) export LEARN_USER=sa ;;
    postgres)  export LEARN_USER=postgres ;;
    *)         export LEARN_USER=root ;;
  esac
}

echo 'sandbox mode'
clear_lab_env
if lab_own_server; then assert_equal 'sandbox' 'own-server' 'no LEARN_SERVER means sandbox mode'
else echo '  PASS  no LEARN_SERVER means sandbox mode'; fi
assert_equal 'sqlserver postgres mysql mariadb' "$(lab_engines | tr '\n' ' ' | sed 's/ $//')" 'sandbox runs all four engines'
assert_equal '1' "$(lab_sql sqlserver master   'SELECT 1')" 'sqlserver inline query'
assert_equal '1' "$(lab_sql postgres  postgres 'SELECT 1')" 'postgres inline query'
assert_equal '1' "$(lab_sql mysql     learn    'SELECT 1')" 'mysql inline query'
assert_equal '1' "$(lab_sql mariadb   learn    'SELECT 1')" 'mariadb inline query'

echo "own-server mode (pointed at the sandbox's published ports)"
set_lab_env sqlserver 11433
if lab_own_server; then echo '  PASS  LEARN_SERVER means own-server mode'
else assert_equal 'own-server' 'sandbox' 'LEARN_SERVER means own-server mode'; fi
assert_equal 'sqlserver' "$(lab_engines | tr '\n' ' ' | sed 's/ $//')" 'own-server runs only the activated engine'
assert_equal '1' "$(lab_sql sqlserver master 'SELECT 1')" 'sqlserver local-client query'

echo 'failures are loud'
set_lab_env sqlserver 11499   # nothing listening
assert_fails 'could not reach sqlserver' 'unreachable endpoint fails, never returns text' -- \
  lab_sql sqlserver master 'SELECT 1'
set_lab_env sqlserver 11433
assert_fails 'could not reach sqlserver' 'a failed query fails rather than returning the error text' -- \
  lab_sql sqlserver master 'SELECT * FROM no_such_table_here'

clear_lab_env
echo
if [ "$failed" -eq 0 ]; then echo 'lab-sql self-test: all checks passed'; exit 0; fi
echo "lab-sql self-test: $failed check(s) failed"
exit 1

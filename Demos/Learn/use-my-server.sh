#!/usr/bin/env bash
# Point the Learn labs at a database server you already run -- no Docker needed.
#
# SOURCE it (note the leading dot) so the settings land in your shell:
#   . ./use-my-server.sh --engine sqlserver --server myhost --port 1433 --user sa --password 'secret'
#   . ./use-my-server.sh --off       # back to the Docker sandbox
#
# Every lab's schemaquench command then targets your server unchanged: SchemaSmith layers
# SmithySettings_* environment variables over each lab's settings file, so nothing in the
# lab packages or their settings needs editing. The lab helper scripts read the matching
# LEARN_* variables to create databases and read catalogs on the same server.

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  echo "Source this script so its settings land in your shell:" >&2
  echo "  . ./use-my-server.sh --engine sqlserver --server myhost --port 1433 --user sa --password 'secret'" >&2
  echo "Running it instead of sourcing it sets nothing." >&2
  exit 2
fi

lab_engine='' lab_server='' lab_port='' lab_user='' lab_password='' lab_off=0
while [ "$#" -gt 0 ]; do
  case "$1" in
    --engine)   lab_engine="$2"; shift 2 ;;
    --server)   lab_server="$2"; shift 2 ;;
    --port)     lab_port="$2"; shift 2 ;;
    --user)     lab_user="$2"; shift 2 ;;
    --password) lab_password="$2"; shift 2 ;;
    --off)      lab_off=1; shift ;;
    *) echo "Unknown option '$1'." >&2; shift ;;
  esac
done

if [ "$lab_off" -eq 1 ]; then
  unset LEARN_ENGINE LEARN_SERVER LEARN_PORT LEARN_USER LEARN_PASSWORD
  unset SmithySettings_Target__Server SmithySettings_Target__Port
  unset SmithySettings_Target__User SmithySettings_Target__Password
  echo 'Learn labs are back on the Docker sandbox.'
else
  if [ -z "$lab_engine" ] || [ -z "$lab_server" ]; then
    echo 'Usage: . ./use-my-server.sh --engine <sqlserver|postgres|mysql|mariadb> --server <host> [--port <n>] --user <user> --password <pw>'
    echo '       . ./use-my-server.sh --off'
  elif [ "${lab_server#*,}" != "$lab_server" ]; then
    echo "Pass the host on its own and the port separately: --server ${lab_server%%,*} --port ${lab_server#*,}" >&2
  elif [ -z "$lab_user" ] || [ -z "$lab_password" ]; then
    # Windows Authentication isn't expressible here yet: on Windows an environment variable
    # cannot be set to empty (assigning '' deletes it), so a lab settings file's "User" can
    # never be overridden away, and SchemaSmith selects integrated security only when both
    # user and password are empty. Use a SQL login until that product fix ships.
    echo '--user and --password are required for every engine.' >&2
    echo >&2
    echo "Windows Authentication isn't supported by the labs yet: a settings-file credential can't be" >&2
    echo "cleared by an override on Windows, so the lab's own user would still be used. Create a SQL" >&2
    echo 'login for the labs and pass it here. Windows Authentication follows in a later release.' >&2
  else
    case "$lab_engine" in
      sqlserver) [ -n "$lab_port" ] || lab_port=1433 ;;
      postgres)  [ -n "$lab_port" ] || lab_port=5432 ;;
      mysql|mariadb) [ -n "$lab_port" ] || lab_port=3306 ;;
      *) echo "Unknown engine '$lab_engine' (expected sqlserver, postgres, mysql, or mariadb)." >&2; lab_engine='' ;;
    esac
    if [ -n "$lab_engine" ]; then
      export LEARN_ENGINE="$lab_engine"
      export LEARN_SERVER="$lab_server"
      export LEARN_PORT="$lab_port"
      export LEARN_USER="$lab_user"
      export LEARN_PASSWORD="$lab_password"

      export SmithySettings_Target__Server="$lab_server"
      export SmithySettings_Target__Port="$lab_port"
      export SmithySettings_Target__User="$lab_user"
      export SmithySettings_Target__Password="$lab_password"

      echo "Learn labs now target $lab_engine at ${lab_server}:${lab_port} as '$lab_user'."
      echo '  This shell only -- open a new terminal and you will need to source this again.'
      echo "  Run each course's setup script before its labs; it creates that course's databases on your server."
      echo '  Back to the sandbox: . ./use-my-server.sh --off'
    fi
  fi
fi

unset lab_engine lab_server lab_port lab_user lab_password lab_off

-- Ownership-stamp operations for the own-server helper (PostgreSQL). Run connected
-- to a maintenance database (the helper connects to 'postgres'):
--   psql ... -v ON_ERROR_STOP=1 -v op=<check|add|dropIfStamped> -v db="<name>" -f stamp.sql
-- Emits a single token line the caller parses: STAMP_RESULT:<absent|stamped|unstamped|added|dropped|noop>
-- The stamp lives in the database's shared comment (COMMENT ON DATABASE), readable
-- from any connection via shobj_description(oid,'pg_database').
\set stamp SchemaSmith_DemoProvisioned
SELECT
  EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db') AS db_exists,
  (COALESCE((SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = :'db'), '') = :'stamp') AS is_stamped,
  (:'op' = 'check') AS op_check,
  (:'op' = 'add') AS op_add,
  (:'op' = 'dropIfStamped') AS op_drop
\gset

\if :op_check
  \if :db_exists
    \if :is_stamped
      \echo STAMP_RESULT:stamped
    \else
      \echo STAMP_RESULT:unstamped
    \endif
  \else
    \echo STAMP_RESULT:absent
  \endif
\elif :op_add
  COMMENT ON DATABASE :"db" IS :'stamp';
  \echo STAMP_RESULT:added
\elif :op_drop
  \if :is_stamped
    -- DROP DATABASE needs no active connections; terminate other backends first
    -- (portable across PG versions, unlike DROP DATABASE ... WITH (FORCE), PG13+).
    SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = :'db' AND pid <> pg_backend_pid();
    DROP DATABASE IF EXISTS :"db";
    \echo STAMP_RESULT:dropped
  \else
    \echo STAMP_RESULT:noop
  \endif
\else
  \echo STAMP_RESULT:noop
\endif

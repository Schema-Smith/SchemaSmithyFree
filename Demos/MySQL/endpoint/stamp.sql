-- Ownership-stamp operations for the own-server helper (MySQL). The caller passes
-- @db and @op via the client's --init-command; connect with NO default database:
--   mysql ... -N -s --init-command="SET @db='<name>', @op='<check|add|dropIfStamped>'" < stamp.sql
-- MySQL has no database comments, so the stamp is a marker TABLE created inside each
-- provisioned database. The client has no top-level IF, so op-dispatch is branchless:
-- each action is a prepared statement whose text is the real op or a harmless 'DO 0'.
-- Emits a single token line the caller parses (for op=check): STAMP_RESULT:<absent|stamped|unstamped>
SET @stamp := 'SchemaSmith_DemoProvisioned';
SET @db_exists := (SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @db);
SET @is_stamped := (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @db AND table_name = @stamp);

SELECT CONCAT('STAMP_RESULT:',
  CASE WHEN @op <> 'check'   THEN 'noop'
       WHEN @db_exists = 0   THEN 'absent'
       WHEN @is_stamped > 0  THEN 'stamped'
       ELSE 'unstamped' END) AS r;

-- add: create the marker table (idempotent) when op=add; no-op otherwise.
SET @sql := IF(@op = 'add',
  CONCAT('CREATE TABLE IF NOT EXISTS `', @db, '`.`', @stamp, '` (marker TINYINT NOT NULL)'),
  'DO 0');
PREPARE _st FROM @sql; EXECUTE _st; DEALLOCATE PREPARE _st;

-- dropIfStamped: drop the database when op=dropIfStamped AND it carries the marker; no-op otherwise.
SET @sql := IF(@op = 'dropIfStamped' AND @is_stamped > 0,
  CONCAT('DROP DATABASE IF EXISTS `', @db, '`'),
  'DO 0');
PREPARE _st FROM @sql; EXECUTE _st; DEALLOCATE PREPARE _st;

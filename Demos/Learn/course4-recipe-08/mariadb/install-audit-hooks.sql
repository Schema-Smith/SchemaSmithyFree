-- Recipe 6 installed the SIMPLEST recyclebin hook: rename the table aside, rename it back. This recipe
-- AUTHORS a richer, production-honest body against the same contract. On top of the audit trail, the drop
-- hook does what a real soft-drop must do before it sets a table aside:
--   * STRIP the table's own FOREIGN KEYS. On MySQL, FK constraint symbols are schema-scoped, so an archived
--     copy that kept them would collide the next time a table of the same name is created. (Primary-key and
--     index names are per-table on MySQL, so they don't collide and are carried along untouched.) The engine
--     re-adds foreign keys from the model when the table is restored.
--   * CLEAR the product-ownership rows (table + index entries), so the archived copy isn't tracked as owned.
-- The engine already drops INBOUND foreign keys before calling the hook. MySQL has no schema namespace and
-- can't declare default parameter values, so the hooks live in the target database and retention is hard-
-- coded to 90. The audit table doubles as the restore registry. Run once against the target database.
DROP PROCEDURE IF EXISTS SchemaSmith_CustomTableDrop;
DROP PROCEDURE IF EXISTS SchemaSmith_CustomTableRestore;

CREATE TABLE IF NOT EXISTS SchemaSmith_TableDropAudit (
  audit_id       BIGINT AUTO_INCREMENT PRIMARY KEY,
  database_name  VARCHAR(128) NOT NULL,
  table_name     VARCHAR(128) NOT NULL,
  archived_name  VARCHAR(200),
  rows_archived  BIGINT,
  retention_days INT,
  action         VARCHAR(10)  NOT NULL,           -- 'DROP' | 'RESTORE'
  action_at      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  action_by      VARCHAR(128) NOT NULL
);

DELIMITER $$
CREATE PROCEDURE SchemaSmith_CustomTableDrop(IN p_DatabaseName VARCHAR(128), IN p_TableName VARCHAR(128))
BEGIN
  DECLARE v_archived VARCHAR(200);
  DECLARE v_exists   INT DEFAULT 0;
  DECLARE v_done     INT DEFAULT 0;
  DECLARE v_fk       VARCHAR(128);
  DECLARE fk_cur CURSOR FOR
    SELECT constraint_name FROM information_schema.table_constraints
     WHERE table_schema = p_DatabaseName AND table_name = p_TableName AND constraint_type = 'FOREIGN KEY';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

  IF LOCATE('__dropped_', p_TableName) = 0 THEN               -- never recycle an already-archived table
    SELECT COUNT(*) INTO v_exists FROM information_schema.tables
      WHERE table_schema = p_DatabaseName AND table_name = p_TableName;
    IF v_exists > 0 THEN                                      -- already gone -> no-op
      SET @rows = 0;
      SET @cnt = CONCAT('SELECT COUNT(*) INTO @rows FROM `', p_DatabaseName, '`.`', p_TableName, '`');
      PREPARE s FROM @cnt; EXECUTE s; DEALLOCATE PREPARE s;

      -- strip own foreign keys (their symbols are schema-scoped)
      OPEN fk_cur;
      fk_loop: LOOP
        FETCH fk_cur INTO v_fk;
        IF v_done = 1 THEN LEAVE fk_loop; END IF;
        SET @fk = CONCAT('ALTER TABLE `', p_DatabaseName, '`.`', p_TableName, '` DROP FOREIGN KEY `', v_fk, '`');
        PREPARE s FROM @fk; EXECUTE s; DEALLOCATE PREPARE s;
      END LOOP;
      CLOSE fk_cur;

      -- clear ownership rows (table + its index entries) so nothing is tracked under the old name.
      -- SchemaSmith created the ownership table as utf8mb4_unicode_ci; coerce the proc params to match,
      -- or MySQL raises "Illegal mix of collations" against the server-default parameter collation.
      DELETE FROM SchemaSmith_ProductOwnership
        WHERE ObjectSchema = p_DatabaseName COLLATE utf8mb4_unicode_ci
          AND (ObjectName = p_TableName COLLATE utf8mb4_unicode_ci
               OR ObjectName LIKE CONCAT(p_TableName, '.%') COLLATE utf8mb4_unicode_ci);

      SET v_archived = CONCAT(p_TableName, '__dropped_', DATE_FORMAT(NOW(6), '%Y%m%d%H%i%s%f'));
      SET @rn = CONCAT('ALTER TABLE `', p_DatabaseName, '`.`', p_TableName, '` RENAME TO `', p_DatabaseName, '`.`', v_archived, '`');
      PREPARE s FROM @rn; EXECUTE s; DEALLOCATE PREPARE s;

      INSERT INTO SchemaSmith_TableDropAudit (database_name, table_name, archived_name, rows_archived, retention_days, action, action_by)
      VALUES (p_DatabaseName, p_TableName, v_archived, @rows, 90, 'DROP', CURRENT_USER());
    END IF;
  END IF;
END $$
CREATE PROCEDURE SchemaSmith_CustomTableRestore(IN p_DatabaseName VARCHAR(128), IN p_TableName VARCHAR(128))
BEGIN
  DECLARE v_archived VARCHAR(200) DEFAULT NULL;
  DECLARE v_exists   INT DEFAULT 0;
  SELECT archived_name INTO v_archived FROM SchemaSmith_TableDropAudit
    WHERE database_name = p_DatabaseName AND table_name = p_TableName AND action = 'DROP'
    ORDER BY action_at DESC LIMIT 1;
  IF v_archived IS NOT NULL THEN
    SELECT COUNT(*) INTO v_exists FROM information_schema.tables
      WHERE table_schema = p_DatabaseName AND table_name = v_archived;
    IF v_exists > 0 THEN
      SET @rn = CONCAT('ALTER TABLE `', p_DatabaseName, '`.`', v_archived, '` RENAME TO `', p_DatabaseName, '`.`', p_TableName, '`');
      PREPARE s FROM @rn; EXECUTE s; DEALLOCATE PREPARE s;
      INSERT INTO SchemaSmith_TableDropAudit (database_name, table_name, archived_name, action, action_by)
      VALUES (p_DatabaseName, p_TableName, v_archived, 'RESTORE', CURRENT_USER());
    END IF;
  END IF;
END $$
DELIMITER ;

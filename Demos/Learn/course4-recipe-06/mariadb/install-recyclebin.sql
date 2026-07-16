-- Installs the two recyclebin hooks SchemaQuench looks for. When a table is removed from the product,
-- the engine routes its drop through SchemaSmith_CustomTableDrop (if present) instead of a hard DROP;
-- when a table is being added, it calls SchemaSmith_CustomTableRestore first and, if the table comes
-- back, does not recreate it. These hooks "soft-drop" by renaming the table aside, so its structure AND
-- data ride through the rebuild. Run once against the target database.
DROP PROCEDURE IF EXISTS SchemaSmith_CustomTableDrop;
DROP PROCEDURE IF EXISTS SchemaSmith_CustomTableRestore;
DELIMITER $$
CREATE PROCEDURE SchemaSmith_CustomTableDrop(IN p_DatabaseName VARCHAR(128), IN p_TableName VARCHAR(128))
BEGIN
  DECLARE rb VARCHAR(160);
  IF LEFT(p_TableName, 14) <> '__recyclebin__' THEN   -- never recycle a recyclebin table
    SET rb = CONCAT('__recyclebin__', p_TableName);
    SET @d = CONCAT('DROP TABLE IF EXISTS `', p_DatabaseName, '`.`', rb, '`');
    PREPARE s FROM @d; EXECUTE s; DEALLOCATE PREPARE s;
    SET @r = CONCAT('ALTER TABLE `', p_DatabaseName, '`.`', p_TableName, '` RENAME TO `', p_DatabaseName, '`.`', rb, '`');
    PREPARE s FROM @r; EXECUTE s; DEALLOCATE PREPARE s;
  END IF;
END $$
CREATE PROCEDURE SchemaSmith_CustomTableRestore(IN p_DatabaseName VARCHAR(128), IN p_TableName VARCHAR(128))
BEGIN
  DECLARE rb VARCHAR(160);
  DECLARE cnt INT;
  SET rb = CONCAT('__recyclebin__', p_TableName);
  SELECT COUNT(*) INTO cnt FROM information_schema.tables WHERE table_schema = p_DatabaseName AND table_name = rb;
  IF cnt > 0 THEN
    SET @r = CONCAT('ALTER TABLE `', p_DatabaseName, '`.`', rb, '` RENAME TO `', p_DatabaseName, '`.`', p_TableName, '`');
    PREPARE s FROM @r; EXECUTE s; DEALLOCATE PREPARE s;
  END IF;
END $$
DELIMITER ;

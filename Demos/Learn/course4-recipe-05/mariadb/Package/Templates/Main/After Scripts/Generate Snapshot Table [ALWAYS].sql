-- Reads the declared Product table model (the SpecificTable token holds its JSON) and GENERATES a
-- ProductSnapshot table that mirrors Product's columns, then copies the current rows into it. Add a
-- column to Product and re-quench: the generated table grows to match and the next snapshot includes
-- it -- no second declaration to keep in sync. [ALWAYS] = runs every quench.
SET @json = '{{ProductTable}}';

SELECT
  GROUP_CONCAT(CONCAT('`', c, '` ', dtype) ORDER BY ord SEPARATOR ', '),
  GROUP_CONCAT(CONCAT('`', c, '`') ORDER BY ord SEPARATOR ',')
INTO @defs, @list
FROM (
  SELECT ord, REPLACE(col, '`', '') AS c, dtype
  FROM JSON_TABLE(@json, '$.Columns[*]'
       COLUMNS (ord FOR ORDINALITY, col VARCHAR(128) PATH '$.Name', dtype VARCHAR(128) PATH '$.DataType')) jt
) cols;

SET @exists = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'cookbook_r5' AND table_name = 'ProductSnapshot');
SET @ddl = IF(@exists = 0,
  CONCAT('CREATE TABLE ProductSnapshot (SnapshotAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), ', @defs, ')'),
  'SELECT 1');
PREPARE s FROM @ddl; EXECUTE s; DEALLOCATE PREPARE s;

SET @add = (
  SELECT CONCAT('ALTER TABLE ProductSnapshot ', GROUP_CONCAT(CONCAT('ADD COLUMN `', c, '` ', dtype) SEPARATOR ', '))
  FROM (
    SELECT REPLACE(col, '`', '') AS c, dtype
    FROM JSON_TABLE(@json, '$.Columns[*]' COLUMNS (col VARCHAR(128) PATH '$.Name', dtype VARCHAR(128) PATH '$.DataType')) jt
  ) cols
  WHERE NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'cookbook_r5' AND table_name = 'ProductSnapshot' AND column_name = cols.c)
);
SET @add = IF(@add IS NULL, 'SELECT 1', @add);
PREPARE s FROM @add; EXECUTE s; DEALLOCATE PREPARE s;

SET @ins = CONCAT('INSERT INTO ProductSnapshot (SnapshotAt, ', @list, ') SELECT CURRENT_TIMESTAMP(6), ', @list, ' FROM Product');
PREPARE s FROM @ins; EXECUTE s; DEALLOCATE PREPARE s;

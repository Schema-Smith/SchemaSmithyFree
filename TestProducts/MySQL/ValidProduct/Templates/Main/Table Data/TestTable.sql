-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

SET @TableData = '{{TestTableInitialData}}';

INSERT INTO `TestTable` (`TestID`, `ParentID`, `Status`, `SomeText`)
  SELECT `TestID`, `ParentID`, `Status`, `SomeText`
    FROM JSON_TABLE(@TableData, '$[*]' COLUMNS (
      `TestID` INT PATH '$.TestID',
      `ParentID` INT PATH '$.ParentID',
      `Status` SMALLINT PATH '$.Status',
      `SomeText` VARCHAR(2000) PATH '$.SomeText'
    )) AS jt
  ON DUPLICATE KEY UPDATE
    `ParentID` = VALUES(`ParentID`),
    `Status` = VALUES(`Status`),
    `SomeText` = VALUES(`SomeText`);

-- Reads the full model from the TableSchema system token (Module 4) — which includes the
-- custom Extensions metadata you attached — and rebuilds DataCatalog from it on every deploy.
-- Walks tables then columns via JSON_TABLE; records each column's Classification and its table's
-- OwningTeam, for every table that carries an OwningTeam tag (tables without it, like DataCatalog
-- itself, are skipped). Names carry backticks in the model JSON, so they're stripped here.
DELETE FROM `DataCatalog`;

INSERT INTO `DataCatalog` (`TableName`, `ColumnName`, `Classification`, `OwningTeam`)
SELECT REPLACE(jt.TableName, '`', ''),
       REPLACE(jt.ColumnName, '`', ''),
       jt.Classification,
       jt.OwningTeam
  FROM JSON_TABLE('{{TableSchema}}', '$[*]' COLUMNS (
         TableName  VARCHAR(256) PATH '$.Name',
         OwningTeam VARCHAR(64)  PATH '$.Extensions.OwningTeam',
         NESTED PATH '$.Columns[*]' COLUMNS (
           ColumnName     VARCHAR(256) PATH '$.Name',
           Classification VARCHAR(64)  PATH '$.Extensions.Classification'
         )
       )) AS jt
 WHERE jt.OwningTeam IS NOT NULL;

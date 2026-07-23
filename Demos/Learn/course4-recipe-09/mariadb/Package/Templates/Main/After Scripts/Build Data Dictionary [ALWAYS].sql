-- Extensions aren't just inputs to gates and defaults -- they're an authoritative metadata store your own
-- scripts can turn into real work. Here the whole template's table model -- every table, with all its
-- Extensions at every level, via the TableSchema token below -- is shredded into a queryable DataDictionary:
-- one row per column, carrying the table's business metadata and the column's. It runs every quench, so the
-- dictionary stays in sync with what the schema files declare -- the schema is the single source of truth.
-- (Note: token substitution is plain text and expands even inside comments, so we don't spell the token's
--  braces out in prose above -- doing so would inline the whole JSON here and break the script.)
CREATE TABLE IF NOT EXISTS DataDictionary (
  schema_name       VARCHAR(128) NOT NULL,
  table_name        VARCHAR(128) NOT NULL,
  business_domain   VARCHAR(128),
  data_owner        VARCHAR(128),
  column_name       VARCHAR(128) NOT NULL,
  business_name     VARCHAR(128),
  sensitivity_level VARCHAR(64),
  data_steward      VARCHAR(128),
  PRIMARY KEY (schema_name, table_name, column_name)
);

-- shred the model with JSON_TABLE (NESTED PATH walks each table's columns). MySQL has no schema namespace,
-- so the dictionary keys on DATABASE(); table/column names come through backtick-quoted, so strip them.
INSERT INTO DataDictionary (schema_name, table_name, business_domain, data_owner, column_name, business_name, sensitivity_level, data_steward)
SELECT DATABASE(), REPLACE(jt.table_name, '`', ''), jt.business_domain, jt.data_owner,
       REPLACE(jt.column_name, '`', ''), jt.business_name, jt.sensitivity_level, jt.data_steward
FROM JSON_TABLE('{{TableSchema}}', '$[*]' COLUMNS (
       table_name      VARCHAR(128) PATH '$.Name',
       business_domain VARCHAR(128) PATH '$.Extensions.BusinessDomain',
       data_owner      VARCHAR(128) PATH '$.Extensions.DataOwner',
       NESTED PATH '$.Columns[*]' COLUMNS (
         column_name       VARCHAR(128) PATH '$.Name',
         business_name     VARCHAR(128) PATH '$.Extensions.BusinessName',
         sensitivity_level VARCHAR(64)  PATH '$.Extensions.SensitivityLevel',
         data_steward      VARCHAR(128) PATH '$.Extensions.DataSteward'
       )
     )) AS jt
ON DUPLICATE KEY UPDATE
  business_domain = VALUES(business_domain), data_owner = VALUES(data_owner),
  business_name = VALUES(business_name), sensitivity_level = VALUES(sensitivity_level), data_steward = VALUES(data_steward);

-- drop dictionary rows for columns no longer in the model
DELETE dd FROM DataDictionary dd
WHERE dd.schema_name = DATABASE() AND NOT EXISTS (
  SELECT 1 FROM JSON_TABLE('{{TableSchema}}', '$[*]' COLUMNS (
           t VARCHAR(128) PATH '$.Name',
           NESTED PATH '$.Columns[*]' COLUMNS ( c VARCHAR(128) PATH '$.Name' )
         )) AS m
  WHERE REPLACE(m.t, '`', '') COLLATE utf8mb4_general_ci = dd.table_name AND REPLACE(m.c, '`', '') COLLATE utf8mb4_general_ci = dd.column_name
);

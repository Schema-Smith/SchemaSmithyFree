-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_TableQuench//

CREATE PROCEDURE SchemaSmith_TableQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_TableDefinitions LONGTEXT,
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT,
    IN p_DropTablesRemovedFromProduct TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- Combined TableQuench procedure for bootstrapping and manual table updates.
    -- Calls the individual procedures in sequence:
    -- 1. ParseTableJson - creates temp tables from JSON
    -- 2. MissingTableAndColumnQuench - creates new tables and columns
    -- 3. ModifiedTableQuench - modifies existing columns
    -- 4. (Future) MissingIndexesAndConstraintsQuench - creates indexes and constraints

    -- Step 1: Parse JSON into temp tables
    CALL SchemaSmith_ParseTableJson(p_DatabaseName, p_TableDefinitions);

    -- Step 2: Create missing tables and add missing columns
    CALL SchemaSmith_MissingTableAndColumnQuench(p_DatabaseName, p_WhatIf);

    -- Step 3: Modify existing tables (column changes, drops if configured)
    CALL SchemaSmith_ModifiedTableQuench(p_ProductName, p_DatabaseName, p_WhatIf, p_DropTablesRemovedFromProduct, 1, 1, 1, 1);

    -- Step 4: Create missing indexes and check constraints (no FKs)
    CALL SchemaSmith_MissingIndexesAndConstraintsQuench(p_ProductName, p_DatabaseName, p_WhatIf, p_DropUnknownIndexes, 1, 1);

    -- Step 5: Create/modify/drop foreign keys
    CALL SchemaSmith_ForeignKeyQuench(p_ProductName, p_DatabaseName, p_WhatIf, p_DropUnknownIndexes, 1);

    -- Cleanup temp tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Tables;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Columns;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Indexes;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ForeignKeys;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CheckConstraints;

END//

DELIMITER ;

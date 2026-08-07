-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('[SchemaSmith].[ValidateIndexedViewOwnership]', 'P') IS NOT NULL DROP PROCEDURE [SchemaSmith].[ValidateIndexedViewOwnership]
GO
CREATE PROCEDURE [SchemaSmith].[ValidateIndexedViewOwnership]
    @ProductName NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT s.name AS SchemaName, v.name AS ViewName
    FROM sys.views v
    INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
    WHERE OBJECTPROPERTY(v.object_id, 'IsIndexed') = 1
    AND NOT EXISTS (
        SELECT 1 FROM sys.extended_properties ep
        WHERE ep.major_id = v.object_id AND ep.minor_id = 0
        AND ep.name = 'SchemaSmith_Product'
    )
    AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'SchemaSmith')
    AND v.is_ms_shipped = 0
    ORDER BY s.name, v.name;
END;

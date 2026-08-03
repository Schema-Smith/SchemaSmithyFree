-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('[SchemaSmith].[FixupIndexedViewOwnership]', 'P') IS NOT NULL DROP PROCEDURE [SchemaSmith].[FixupIndexedViewOwnership]
GO
CREATE PROCEDURE [SchemaSmith].[FixupIndexedViewOwnership]
    @ProductName NVARCHAR(200),
    @SchemaName NVARCHAR(200),
    @ViewName NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @objectId INT = OBJECT_ID(QUOTENAME(@SchemaName) + '.' + QUOTENAME(@ViewName));
    IF @objectId IS NULL RETURN;

    IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = @objectId AND minor_id = 0 AND name = 'SchemaSmith_Product')
        EXEC sp_addextendedproperty 'SchemaSmith_Product', @ProductName, 'SCHEMA', @SchemaName, 'VIEW', @ViewName;
    ELSE
        EXEC sp_updateextendedproperty 'SchemaSmith_Product', @ProductName, 'SCHEMA', @SchemaName, 'VIEW', @ViewName;
END;

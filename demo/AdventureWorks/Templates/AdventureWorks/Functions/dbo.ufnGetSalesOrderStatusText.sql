SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

DECLARE @v_SearchTerm VARCHAR(2000) = '%ufnGetSalesOrderStatusText%'
DECLARE @v_SQL VARCHAR(MAX) = (SELECT STRING_AGG(Task, ';' + CHAR(13) + CHAR(10)) 
                                 FROM (SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(cc.parent_object_id) + '].[' + OBJECT_NAME(cc.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + OBJECT_NAME(cc.[name]) + ']' AS Task
                                         FROM sys.check_constraints cc
                                         WHERE cc.[definition] LIKE @v_SearchTerm
                                            OR EXISTS (SELECT *
                                                         FROM sys.computed_columns cc2
                                                         WHERE cc2.[definition] LIKE @v_SearchTerm
                                                           AND cc2.[object_id] = cc.parent_object_id
                                                           AND cc2.column_id = cc.parent_column_id)
                                       UNION ALL
                                       SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(dc.parent_object_id) + '].[' + OBJECT_NAME(dc.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + OBJECT_NAME(dc.[name]) + ']'
                                         FROM sys.default_constraints dc
                                         WHERE dc.[definition] LIKE @v_SearchTerm
                                            OR EXISTS (SELECT *
                                                         FROM sys.computed_columns cc
                                                         WHERE cc.[definition] LIKE @v_SearchTerm
                                                           AND cc.[object_id] = dc.parent_object_id
                                                           AND cc.column_id = dc.parent_column_id)
                                       UNION ALL
                                       SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '].[' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + OBJECT_NAME(fk.[name]) + ']'
                                         FROM sys.foreign_keys fk
                                         WHERE EXISTS (SELECT *
                                                         FROM sys.computed_columns cc
                                                         JOIN sys.foreign_key_columns fc ON fk.[object_id] = fk.[object_id]
                                                                                        AND ((fc.parent_object_id = cc.[object_id] AND fc.parent_column_id = cc.column_id)
                                                                                          OR (fc.referenced_object_id = cc.[object_id] AND fc.referenced_column_id = cc.column_id))
                                                         WHERE cc.[definition] LIKE @v_SearchTerm)
                                       UNION ALL
                                       SELECT 'DROP INDEX IF EXISTS [' + si.[name] + '] ON [' + OBJECT_SCHEMA_NAME(si.[object_id]) + '].[' + OBJECT_NAME(si.[object_id]) + ']'
                                         FROM sys.indexes si
                                         WHERE si.filter_definition LIKE @v_SearchTerm
                                            OR EXISTS (SELECT *
                                                         FROM sys.computed_columns cc
                                                         JOIN sys.index_columns ic ON ic.[object_id] = si.[object_id]
                                                                                  AND ic.index_id = si.index_id
                                                                                  AND ic.column_id = cc.column_id
                                                         WHERE cc.[definition] LIKE @v_SearchTerm
                                                           AND cc.[object_id] = si.[object_id])
                                       UNION ALL
                                       SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(cc.[object_id]) + '].[' + OBJECT_NAME(cc.[object_id]) + '] DROP COLUMN IF EXISTS [' + cc.[name] + ']'
                                         FROM sys.computed_columns cc
                                         WHERE cc.[definition] LIKE @v_SearchTerm) x) + ';'
EXEC(@v_SQL) -- Remove any dependencies before updating the function
GO

CREATE OR ALTER   FUNCTION [dbo].[ufnGetSalesOrderStatusText](@Status [tinyint])
RETURNS [nvarchar](15) 
AS 

-- Returns the sales order status text representation for the status value.
BEGIN
    DECLARE @ret [nvarchar](15);

    SET @ret = 
        CASE @Status
            WHEN 1 THEN 'In process'
            WHEN 2 THEN 'Approved'
            WHEN 3 THEN 'Backordered'
            WHEN 4 THEN 'Rejected'
            WHEN 5 THEN 'Shipped'
            WHEN 6 THEN 'Cancelled'
            ELSE '** Invalid **'
        END;
    
    RETURN @ret
END;

GO

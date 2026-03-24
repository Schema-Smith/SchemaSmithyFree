SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

DECLARE @v_SearchTerm VARCHAR(2000) = '%ufnGetContactInformation%'
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

CREATE OR ALTER   FUNCTION [dbo].[ufnGetContactInformation](@PersonID int)
RETURNS @retContactInformation TABLE 
(
    -- Columns returned by the function
    [PersonID] int NOT NULL, 
    [FirstName] [nvarchar](50) NULL, 
    [LastName] [nvarchar](50) NULL, 
	[JobTitle] [nvarchar](50) NULL,
    [BusinessEntityType] [nvarchar](50) NULL
)
AS 

-- Returns the first name, last name, job title and business entity type for the specified contact.
-- Since a contact can serve multiple roles, more than one row may be returned.
BEGIN
	IF @PersonID IS NOT NULL 
		BEGIN
		IF EXISTS(SELECT * FROM [HumanResources].[Employee] e 
					WHERE e.[BusinessEntityID] = @PersonID) 
			INSERT INTO @retContactInformation
				SELECT @PersonID, p.FirstName, p.LastName, e.[JobTitle], 'Employee'
				FROM [HumanResources].[Employee] AS e
					INNER JOIN [Person].[Person] p
					ON p.[BusinessEntityID] = e.[BusinessEntityID]
				WHERE e.[BusinessEntityID] = @PersonID;

		IF EXISTS(SELECT * FROM [Purchasing].[Vendor] AS v
					INNER JOIN [Person].[BusinessEntityContact] bec 
					ON bec.[BusinessEntityID] = v.[BusinessEntityID]
					WHERE bec.[PersonID] = @PersonID)
			INSERT INTO @retContactInformation
				SELECT @PersonID, p.FirstName, p.LastName, ct.[Name], 'Vendor Contact' 
				FROM [Purchasing].[Vendor] AS v
					INNER JOIN [Person].[BusinessEntityContact] bec 
					ON bec.[BusinessEntityID] = v.[BusinessEntityID]
					INNER JOIN [Person].ContactType ct
					ON ct.[ContactTypeID] = bec.[ContactTypeID]
					INNER JOIN [Person].[Person] p
					ON p.[BusinessEntityID] = bec.[PersonID]
				WHERE bec.[PersonID] = @PersonID;
		
		IF EXISTS(SELECT * FROM [Sales].[Store] AS s
					INNER JOIN [Person].[BusinessEntityContact] bec 
					ON bec.[BusinessEntityID] = s.[BusinessEntityID]
					WHERE bec.[PersonID] = @PersonID)
			INSERT INTO @retContactInformation
				SELECT @PersonID, p.FirstName, p.LastName, ct.[Name], 'Store Contact' 
				FROM [Sales].[Store] AS s
					INNER JOIN [Person].[BusinessEntityContact] bec 
					ON bec.[BusinessEntityID] = s.[BusinessEntityID]
					INNER JOIN [Person].ContactType ct
					ON ct.[ContactTypeID] = bec.[ContactTypeID]
					INNER JOIN [Person].[Person] p
					ON p.[BusinessEntityID] = bec.[PersonID]
				WHERE bec.[PersonID] = @PersonID;

		IF EXISTS(SELECT * FROM [Person].[Person] AS p
					INNER JOIN [Sales].[Customer] AS c
					ON c.[PersonID] = p.[BusinessEntityID]
					WHERE p.[BusinessEntityID] = @PersonID AND c.[StoreID] IS NULL) 
			INSERT INTO @retContactInformation
				SELECT @PersonID, p.FirstName, p.LastName, NULL, 'Consumer' 
				FROM [Person].[Person] AS p
					INNER JOIN [Sales].[Customer] AS c
					ON c.[PersonID] = p.[BusinessEntityID]
					WHERE p.[BusinessEntityID] = @PersonID AND c.[StoreID] IS NULL; 
		END

	RETURN;
END;

GO

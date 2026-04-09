
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.ShoppingCartItem.tabledata}}';


SET IDENTITY_INSERT [Sales].[ShoppingCartItem] ON;
MERGE INTO [Sales].[ShoppingCartItem] AS Target
USING (
  SELECT [DateCreated],[ModifiedDate],[ProductID],[Quantity],[ShoppingCartID],[ShoppingCartItemID]
    FROM OPENJSON(@v_json)
    WITH (
           [DateCreated] DATETIME,
           [ModifiedDate] DATETIME,
           [ProductID] INT,
           [Quantity] INT,
           [ShoppingCartID] NVARCHAR(50),
           [ShoppingCartItemID] INT
    )
) AS Source
ON Source.[ShoppingCartItemID] = Target.[ShoppingCartItemID]

WHEN MATCHED AND (NOT (Target.[DateCreated] = Source.[DateCreated] OR (Target.[DateCreated] IS NULL AND Source.[DateCreated] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[Quantity] = Source.[Quantity] OR (Target.[Quantity] IS NULL AND Source.[Quantity] IS NULL)) OR NOT (Target.[ShoppingCartID] = Source.[ShoppingCartID] OR (Target.[ShoppingCartID] IS NULL AND Source.[ShoppingCartID] IS NULL))) THEN
  UPDATE SET
        [DateCreated] = Source.[DateCreated],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [Quantity] = Source.[Quantity],
        [ShoppingCartID] = Source.[ShoppingCartID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [DateCreated],
        [ModifiedDate],
        [ProductID],
        [Quantity],
        [ShoppingCartID],
        [ShoppingCartItemID]
   ) VALUES (
         Source.[DateCreated],
        Source.[ModifiedDate],
        Source.[ProductID],
        Source.[Quantity],
        Source.[ShoppingCartID],
        Source.[ShoppingCartItemID]
   )
 ;
SET IDENTITY_INSERT [Sales].[ShoppingCartItem] OFF;

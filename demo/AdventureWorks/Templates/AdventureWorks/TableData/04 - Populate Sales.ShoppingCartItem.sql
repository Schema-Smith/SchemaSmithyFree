DECLARE @v_json NVARCHAR(MAX) = '[
{"DateCreated":"2013-11-09T17:54:07.603","ModifiedDate":"2013-11-09T17:54:07.603","ProductID":862,"Quantity":3,"ShoppingCartID":"14951","ShoppingCartItemID":2},
{"DateCreated":"2013-11-09T17:54:07.603","ModifiedDate":"2013-11-09T17:54:07.603","ProductID":881,"Quantity":4,"ShoppingCartID":"20621","ShoppingCartItemID":4},
{"DateCreated":"2013-11-09T17:54:07.603","ModifiedDate":"2013-11-09T17:54:07.603","ProductID":874,"Quantity":7,"ShoppingCartID":"20621","ShoppingCartItemID":5}
]';

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
WHEN MATCHED AND (NOT (Target.[DateCreated] = Source.[DateCreated] OR (Target.[DateCreated] IS NULL AND Source.[DateCreated] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) AND NOT (Target.[Quantity] = Source.[Quantity] OR (Target.[Quantity] IS NULL AND Source.[Quantity] IS NULL)) AND NOT (Target.[ShoppingCartID] = Source.[ShoppingCartID] OR (Target.[ShoppingCartID] IS NULL AND Source.[ShoppingCartID] IS NULL))) THEN
  UPDATE SET
        [DateCreated] = Source.[DateCreated],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [Quantity] = Source.[Quantity],
        [ShoppingCartID] = Source.[ShoppingCartID]
WHEN NOT MATCHED THEN
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
  );
SET IDENTITY_INSERT [Sales].[ShoppingCartItem] OFF; 

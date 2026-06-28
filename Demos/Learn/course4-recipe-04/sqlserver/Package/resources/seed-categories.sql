-- Reference data kept in its own file and embedded into a deploy script via the <*File*> token.
-- Idempotent: only inserts categories that aren't already present.
MERGE INTO dbo.Category AS t
USING (VALUES (N'Books'), (N'Electronics'), (N'Garden')) AS s (CategoryName)
   ON t.CategoryName = s.CategoryName
WHEN NOT MATCHED THEN INSERT (CategoryName) VALUES (s.CategoryName);

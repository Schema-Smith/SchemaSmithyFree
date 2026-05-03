SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   PROCEDURE [dbo].[film_not_in_stock]
    @film_id INT,
    @store_id INT,
    @count INT OUTPUT
AS

BEGIN
    SET NOCOUNT ON;

    SELECT i.[inventory_id]
    FROM [dbo].[inventory] i
    WHERE i.[film_id] = @film_id
      AND i.[store_id] = @store_id
      AND [dbo].[inventory_in_stock](i.[inventory_id]) = 0;

    SET @count = @@ROWCOUNT;
END

GO

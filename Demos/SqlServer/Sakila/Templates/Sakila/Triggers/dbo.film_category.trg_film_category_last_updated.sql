SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_film_category_last_updated]
ON [dbo].[film_category]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE t SET t.[last_update] = GETDATE()
    FROM [dbo].[film_category] t
    INNER JOIN inserted i ON t.[film_id] = i.[film_id] AND t.[category_id] = i.[category_id];
END

GO

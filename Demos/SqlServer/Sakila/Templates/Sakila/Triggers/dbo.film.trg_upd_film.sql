SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_upd_film]
ON [dbo].[film]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE ft
    SET ft.[title] = i.[title],
        ft.[description] = i.[description],
        ft.[film_id] = i.[film_id]
    FROM [dbo].[film_text] ft
    INNER JOIN inserted i ON ft.[film_id] = i.[film_id]
    INNER JOIN deleted d ON ft.[film_id] = d.[film_id]
    WHERE d.[title] != i.[title]
       OR d.[description] != i.[description];
END

GO

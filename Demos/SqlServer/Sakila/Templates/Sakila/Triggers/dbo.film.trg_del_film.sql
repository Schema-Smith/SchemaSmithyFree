SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_del_film]
ON [dbo].[film]
AFTER DELETE
AS

BEGIN
    SET NOCOUNT ON;
    DELETE ft
    FROM [dbo].[film_text] ft
    INNER JOIN deleted d ON ft.[film_id] = d.[film_id];
END

GO

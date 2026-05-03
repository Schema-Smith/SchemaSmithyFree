SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_ins_film]
ON [dbo].[film]
AFTER INSERT
AS

BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[film_text] ([film_id], [title], [description])
    SELECT i.[film_id], i.[title], i.[description]
    FROM inserted i;
END

GO

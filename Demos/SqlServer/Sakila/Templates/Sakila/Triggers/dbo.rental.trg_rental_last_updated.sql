SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_rental_last_updated]
ON [dbo].[rental]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE t SET t.[last_update] = GETDATE()
    FROM [dbo].[rental] t
    INNER JOIN inserted i ON t.[rental_id] = i.[rental_id];
END

GO

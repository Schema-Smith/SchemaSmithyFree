SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_staff_last_updated]
ON [dbo].[staff]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE t SET t.[last_update] = GETDATE()
    FROM [dbo].[staff] t
    INNER JOIN inserted i ON t.[staff_id] = i.[staff_id];
END

GO

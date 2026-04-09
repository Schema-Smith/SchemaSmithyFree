SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_inventory_last_updated]
ON [dbo].[inventory]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE t SET t.[last_update] = GETDATE()
    FROM [dbo].[inventory] t
    INNER JOIN inserted i ON t.[inventory_id] = i.[inventory_id];
END

GO

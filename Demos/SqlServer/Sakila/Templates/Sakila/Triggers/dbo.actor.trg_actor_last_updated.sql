SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_actor_last_updated]
ON [dbo].[actor]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE t SET t.[last_update] = GETDATE()
    FROM [dbo].[actor] t
    INNER JOIN inserted i ON t.[actor_id] = i.[actor_id];
END

GO

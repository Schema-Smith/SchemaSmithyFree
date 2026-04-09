SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_film_actor_last_updated]
ON [dbo].[film_actor]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE t SET t.[last_update] = GETDATE()
    FROM [dbo].[film_actor] t
    INNER JOIN inserted i ON t.[actor_id] = i.[actor_id] AND t.[film_id] = i.[film_id];
END

GO

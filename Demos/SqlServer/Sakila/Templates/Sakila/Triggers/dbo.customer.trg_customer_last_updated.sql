SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   TRIGGER [dbo].[trg_customer_last_updated]
ON [dbo].[customer]
AFTER UPDATE
AS

BEGIN
    SET NOCOUNT ON;
    UPDATE t SET t.[last_update] = GETDATE()
    FROM [dbo].[customer] t
    INNER JOIN inserted i ON t.[customer_id] = i.[customer_id];
END

GO

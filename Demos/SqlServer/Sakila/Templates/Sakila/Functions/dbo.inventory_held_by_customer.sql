SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   FUNCTION [dbo].[inventory_held_by_customer]
(
    @p_inventory_id INT
)
RETURNS INT
AS

BEGIN
    DECLARE @v_customer_id INT;

    SELECT @v_customer_id = [customer_id]
    FROM [dbo].[rental]
    WHERE [return_date] IS NULL
      AND [inventory_id] = @p_inventory_id;

    RETURN @v_customer_id;
END

GO

SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   FUNCTION [dbo].[inventory_in_stock]
(
    @p_inventory_id INT
)
RETURNS BIT
AS

BEGIN
    DECLARE @v_rentals INT;
    DECLARE @v_out INT;

    SELECT @v_rentals = COUNT(*)
    FROM [dbo].[rental]
    WHERE [inventory_id] = @p_inventory_id;

    IF @v_rentals = 0
        RETURN 1;

    SELECT @v_out = COUNT([rental_id])
    FROM [dbo].[inventory] i
    LEFT JOIN [dbo].[rental] r ON i.[inventory_id] = r.[inventory_id]
    WHERE i.[inventory_id] = @p_inventory_id
      AND r.[return_date] IS NULL;

    IF @v_out > 0
        RETURN 0;

    RETURN 1;
END

GO

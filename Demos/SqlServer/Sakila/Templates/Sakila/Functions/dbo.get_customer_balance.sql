SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   FUNCTION [dbo].[get_customer_balance]
(
    @p_customer_id INT,
    @p_effective_date DATETIME
)
RETURNS DECIMAL(5,2)
AS

BEGIN
    DECLARE @v_rentfees DECIMAL(5,2);
    DECLARE @v_overfees INT;
    DECLARE @v_payments DECIMAL(5,2);

    SELECT @v_rentfees = ISNULL(SUM(f.[rental_rate]), 0)
    FROM [dbo].[film] f
    INNER JOIN [dbo].[inventory] i ON f.[film_id] = i.[film_id]
    INNER JOIN [dbo].[rental] r ON i.[inventory_id] = r.[inventory_id]
    WHERE r.[rental_date] <= @p_effective_date
      AND r.[customer_id] = @p_customer_id;

    SELECT @v_overfees = ISNULL(SUM(
        CASE WHEN DATEDIFF(DAY, r.[rental_date], r.[return_date]) > f.[rental_duration]
            THEN DATEDIFF(DAY, r.[rental_date], r.[return_date]) - f.[rental_duration]
            ELSE 0
        END), 0)
    FROM [dbo].[rental] r
    INNER JOIN [dbo].[inventory] i ON r.[inventory_id] = i.[inventory_id]
    INNER JOIN [dbo].[film] f ON i.[film_id] = f.[film_id]
    WHERE r.[rental_date] <= @p_effective_date
      AND r.[customer_id] = @p_customer_id;

    SELECT @v_payments = ISNULL(SUM(p.[amount]), 0)
    FROM [dbo].[payment] p
    WHERE p.[payment_date] <= @p_effective_date
      AND p.[customer_id] = @p_customer_id;

    RETURN @v_rentfees + @v_overfees - @v_payments;
END

GO

SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   PROCEDURE [dbo].[rewards_report]
    @min_monthly_purchases TINYINT,
    @min_dollar_amount_purchased DECIMAL(10,2)
AS

BEGIN
    SET NOCOUNT ON;

    DECLARE @last_month_start DATE;
    DECLARE @last_month_end DATE;
    DECLARE @count_rewardees INT;

    IF @min_monthly_purchases = 0
    BEGIN
        RAISERROR('Minimum monthly purchases parameter must be > 0', 16, 1);
        RETURN;
    END

    IF @min_dollar_amount_purchased = 0.00
    BEGIN
        RAISERROR('Minimum monthly dollar amount purchased parameter must be > $0.00', 16, 1);
        RETURN;
    END

    SET @last_month_start = DATEADD(MONTH, -1, GETDATE());
    SET @last_month_start = DATEFROMPARTS(YEAR(@last_month_start), MONTH(@last_month_start), 1);
    SET @last_month_end = EOMONTH(@last_month_start);

    CREATE TABLE #tmpCustomer (customer_id INT NOT NULL PRIMARY KEY);

    INSERT INTO #tmpCustomer (customer_id)
    SELECT p.[customer_id]
    FROM [dbo].[payment] p
    WHERE CAST(p.[payment_date] AS DATE) BETWEEN @last_month_start AND @last_month_end
    GROUP BY p.[customer_id]
    HAVING SUM(p.[amount]) > @min_dollar_amount_purchased
       AND COUNT(p.[customer_id]) > @min_monthly_purchases;

    SELECT @count_rewardees = COUNT(*) FROM #tmpCustomer;

    SELECT c.*
    FROM #tmpCustomer t
    INNER JOIN [dbo].[customer] c ON t.[customer_id] = c.[customer_id];

    DROP TABLE #tmpCustomer;
END

GO

-- Idempotent datafix: back up and correct May-2026 OrderItem prices.
-- Safe to re-run: CREATE TABLE is guarded; INSERT skips already-backed-up rows;
-- UPDATE skips already-correct rows.

IF OBJECT_ID('datafix.OrderItem_PriceFix_Backup') IS NULL
    CREATE TABLE datafix.OrderItem_PriceFix_Backup (
        OrderItemId INT         NOT NULL CONSTRAINT PK_OrderItem_PriceFix_Backup PRIMARY KEY,
        OldUnitPrice DECIMAL(10,2) NOT NULL,
        BackedUpAt   DATETIME2  NOT NULL DEFAULT SYSUTCDATETIME()
    );

-- Back up only affected rows not already in the backup table
INSERT INTO datafix.OrderItem_PriceFix_Backup (OrderItemId, OldUnitPrice)
SELECT oi.OrderItemId, oi.UnitPrice
FROM dbo.OrderItem oi
JOIN dbo.SalesOrder so ON so.OrderId = oi.OrderId
WHERE so.OrderDate >= '2026-05-01' AND so.OrderDate < '2026-06-01'
  AND NOT EXISTS (
        SELECT 1
        FROM datafix.OrderItem_PriceFix_Backup b
        WHERE b.OrderItemId = oi.OrderItemId
      );

-- Correct prices where the stored value still differs from the intended single-discount
UPDATE oi
SET    oi.UnitPrice = ROUND(p.UnitPrice * 0.90, 2)
FROM   dbo.OrderItem oi
JOIN   dbo.Product p                  ON p.ProductId   = oi.ProductId
JOIN   datafix.OrderItem_PriceFix_Backup b ON b.OrderItemId = oi.OrderItemId
WHERE  oi.UnitPrice <> ROUND(p.UnitPrice * 0.90, 2);

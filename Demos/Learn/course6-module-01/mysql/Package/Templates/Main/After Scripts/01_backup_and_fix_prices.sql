-- Idempotent datafix: back up and correct May-2026 OrderItem prices.
-- Safe to re-run: CREATE TABLE IF NOT EXISTS; INSERT skips already-backed-up rows;
-- UPDATE skips already-correct rows.

CREATE TABLE IF NOT EXISTS `OrderItem_PriceFix_Backup` (
    `OrderItemId`  INT           NOT NULL,
    `OldUnitPrice` DECIMAL(10,2) NOT NULL,
    `BackedUpAt`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_OrderItem_PriceFix_Backup` PRIMARY KEY (`OrderItemId`)
) ENGINE=InnoDB;

-- Back up only affected rows not already in the backup table
INSERT INTO `OrderItem_PriceFix_Backup` (`OrderItemId`, `OldUnitPrice`)
SELECT oi.`OrderItemId`, oi.`UnitPrice`
FROM   `OrderItem`  oi
JOIN   `SalesOrder` so ON so.`OrderId` = oi.`OrderId`
WHERE  so.`OrderDate` >= '2026-05-01' AND so.`OrderDate` < '2026-06-01'
  AND  NOT EXISTS (
           SELECT 1
           FROM   `OrderItem_PriceFix_Backup` b
           WHERE  b.`OrderItemId` = oi.`OrderItemId`
       );

-- Correct prices where the stored value still differs from the intended single-discount
UPDATE `OrderItem`  oi
JOIN   `Product`    p ON p.`ProductId`   = oi.`ProductId`
JOIN   `OrderItem_PriceFix_Backup` b ON b.`OrderItemId` = oi.`OrderItemId`
SET    oi.`UnitPrice` = ROUND(p.`UnitPrice` * 0.90, 2)
WHERE  oi.`UnitPrice` <> ROUND(p.`UnitPrice` * 0.90, 2);

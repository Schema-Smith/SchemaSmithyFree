-- Idempotent datafix: back up and correct May-2026 orderitem prices.
-- Safe to re-run: CREATE TABLE IF NOT EXISTS; INSERT skips already-backed-up rows;
-- UPDATE skips already-correct rows.

CREATE TABLE IF NOT EXISTS public.orderitem_pricefix_backup (
    orderitemid   INTEGER        NOT NULL CONSTRAINT pk_orderitem_pricefix_backup PRIMARY KEY,
    oldunitprice  NUMERIC(10,2)  NOT NULL,
    backedup_at   TIMESTAMPTZ    NOT NULL DEFAULT NOW()
);

-- Back up only affected rows not already in the backup table
INSERT INTO public.orderitem_pricefix_backup (orderitemid, oldunitprice)
SELECT oi.orderitemid, oi.unitprice
FROM   orderitem  oi
JOIN   salesorder so ON so.orderid = oi.orderid
WHERE  so.orderdate >= '2026-05-01' AND so.orderdate < '2026-06-01'
  AND  NOT EXISTS (
           SELECT 1
           FROM   public.orderitem_pricefix_backup b
           WHERE  b.orderitemid = oi.orderitemid
       );

-- Correct prices where the stored value still differs from the intended single-discount
UPDATE orderitem oi
SET    unitprice = ROUND(p.unitprice * 0.90::numeric, 2)
FROM   product p, public.orderitem_pricefix_backup b
WHERE  p.productid = oi.productid
  AND  b.orderitemid = oi.orderitemid
  AND  oi.unitprice <> ROUND(p.unitprice * 0.90::numeric, 2);

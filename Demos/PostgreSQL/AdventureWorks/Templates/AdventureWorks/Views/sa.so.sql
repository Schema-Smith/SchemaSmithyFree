CREATE OR REPLACE VIEW "sa"."so" AS
 SELECT specialofferid AS id,
    specialofferid,
    description,
    discountpct,
    type,
    category,
    startdate,
    enddate,
    minqty,
    maxqty,
    rowguid,
    modifieddate
   FROM sales.specialoffer;
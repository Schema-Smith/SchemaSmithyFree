CREATE OR REPLACE VIEW "sa"."tr" AS
 SELECT salestaxrateid AS id,
    salestaxrateid,
    stateprovinceid,
    taxtype,
    taxrate,
    name,
    rowguid,
    modifieddate
   FROM sales.salestaxrate;
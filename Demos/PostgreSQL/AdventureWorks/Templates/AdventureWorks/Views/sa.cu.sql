CREATE OR REPLACE VIEW "sa"."cu" AS
 SELECT currencycode AS id,
    currencycode,
    name,
    modifieddate
   FROM sales.currency;
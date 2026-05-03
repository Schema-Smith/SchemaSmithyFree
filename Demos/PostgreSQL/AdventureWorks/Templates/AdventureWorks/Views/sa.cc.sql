CREATE OR REPLACE VIEW "sa"."cc" AS
 SELECT creditcardid AS id,
    creditcardid,
    cardtype,
    cardnumber,
    expmonth,
    expyear,
    modifieddate
   FROM sales.creditcard;
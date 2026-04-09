CREATE OR REPLACE VIEW "sa"."cr" AS
 SELECT currencyrateid,
    currencyratedate,
    fromcurrencycode,
    tocurrencycode,
    averagerate,
    endofdayrate,
    modifieddate
   FROM sales.currencyrate;
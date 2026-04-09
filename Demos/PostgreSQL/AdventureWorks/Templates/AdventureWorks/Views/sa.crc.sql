CREATE OR REPLACE VIEW "sa"."crc" AS
 SELECT countryregioncode,
    currencycode,
    modifieddate
   FROM sales.countryregioncurrency;
CREATE OR REPLACE VIEW "pe"."sp" AS
 SELECT stateprovinceid AS id,
    stateprovinceid,
    stateprovincecode,
    countryregioncode,
    isonlystateprovinceflag,
    name,
    territoryid,
    rowguid,
    modifieddate
   FROM person.stateprovince;
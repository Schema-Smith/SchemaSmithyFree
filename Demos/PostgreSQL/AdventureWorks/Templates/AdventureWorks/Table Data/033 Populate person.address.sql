
DO $$
DECLARE
  v_json JSON = '{{person.address.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "person"."address" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'addressid')::int4 AS "addressid",
           (elem ->> 'addressline1')::varchar(60) AS "addressline1",
           (elem ->> 'addressline2')::varchar(60) AS "addressline2",
           (elem ->> 'city')::varchar(30) AS "city",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'postalcode')::varchar(15) AS "postalcode",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           decode(elem ->> 'spatiallocation', 'base64') AS "spatiallocation",
           (elem ->> 'stateprovinceid')::int4 AS "stateprovinceid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."addressid" = "Target"."addressid"

WHEN MATCHED AND (NOT ("Target"."addressline1" = "Source"."addressline1" OR ("Target"."addressline1" IS NULL AND "Source"."addressline1" IS NULL)) OR NOT ("Target"."addressline2" = "Source"."addressline2" OR ("Target"."addressline2" IS NULL AND "Source"."addressline2" IS NULL)) OR NOT ("Target"."city" = "Source"."city" OR ("Target"."city" IS NULL AND "Source"."city" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."postalcode" = "Source"."postalcode" OR ("Target"."postalcode" IS NULL AND "Source"."postalcode" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."spatiallocation" = "Source"."spatiallocation" OR ("Target"."spatiallocation" IS NULL AND "Source"."spatiallocation" IS NULL)) OR NOT ("Target"."stateprovinceid" = "Source"."stateprovinceid" OR ("Target"."stateprovinceid" IS NULL AND "Source"."stateprovinceid" IS NULL))) THEN
  UPDATE SET
        "addressline1" = "Source"."addressline1",
        "addressline2" = "Source"."addressline2",
        "city" = "Source"."city",
        "modifieddate" = "Source"."modifieddate",
        "postalcode" = "Source"."postalcode",
        "rowguid" = "Source"."rowguid",
        "spatiallocation" = "Source"."spatiallocation",
        "stateprovinceid" = "Source"."stateprovinceid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "addressid",
        "addressline1",
        "addressline2",
        "city",
        "modifieddate",
        "postalcode",
        "rowguid",
        "spatiallocation",
        "stateprovinceid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."addressid",
        "Source"."addressline1",
        "Source"."addressline2",
        "Source"."city",
        "Source"."modifieddate",
        "Source"."postalcode",
        "Source"."rowguid",
        "Source"."spatiallocation",
        "Source"."stateprovinceid"
   )
 ;

SELECT SETVAL('person.address_addressid_seq', (SELECT MAX("addressid") FROM "person"."address")) INTO nextval;

END $$ LANGUAGE plpgsql;


DO $$
DECLARE
  v_json JSON = '{{humanresources.employee.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "humanresources"."employee" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'birthdate')::date AS "birthdate",
           (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'currentflag')::bool AS "currentflag",
           (elem ->> 'gender')::bpchar(1) AS "gender",
           (elem ->> 'hiredate')::date AS "hiredate",
           (elem ->> 'jobtitle')::varchar(50) AS "jobtitle",
           (elem ->> 'loginid')::varchar(256) AS "loginid",
           (elem ->> 'maritalstatus')::bpchar(1) AS "maritalstatus",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'nationalidnumber')::varchar(15) AS "nationalidnumber",
           (elem ->> 'organizationnode')::varchar AS "organizationnode",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'salariedflag')::bool AS "salariedflag",
           (elem ->> 'sickleavehours')::int2 AS "sickleavehours",
           (elem ->> 'vacationhours')::int2 AS "vacationhours"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid"

WHEN MATCHED AND (NOT ("Target"."birthdate" = "Source"."birthdate" OR ("Target"."birthdate" IS NULL AND "Source"."birthdate" IS NULL)) OR NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."currentflag" = "Source"."currentflag" OR ("Target"."currentflag" IS NULL AND "Source"."currentflag" IS NULL)) OR NOT ("Target"."gender" = "Source"."gender" OR ("Target"."gender" IS NULL AND "Source"."gender" IS NULL)) OR NOT ("Target"."hiredate" = "Source"."hiredate" OR ("Target"."hiredate" IS NULL AND "Source"."hiredate" IS NULL)) OR NOT ("Target"."jobtitle" = "Source"."jobtitle" OR ("Target"."jobtitle" IS NULL AND "Source"."jobtitle" IS NULL)) OR NOT ("Target"."loginid" = "Source"."loginid" OR ("Target"."loginid" IS NULL AND "Source"."loginid" IS NULL)) OR NOT ("Target"."maritalstatus" = "Source"."maritalstatus" OR ("Target"."maritalstatus" IS NULL AND "Source"."maritalstatus" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."nationalidnumber" = "Source"."nationalidnumber" OR ("Target"."nationalidnumber" IS NULL AND "Source"."nationalidnumber" IS NULL)) OR NOT ("Target"."organizationnode" = "Source"."organizationnode" OR ("Target"."organizationnode" IS NULL AND "Source"."organizationnode" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."salariedflag" = "Source"."salariedflag" OR ("Target"."salariedflag" IS NULL AND "Source"."salariedflag" IS NULL)) OR NOT ("Target"."sickleavehours" = "Source"."sickleavehours" OR ("Target"."sickleavehours" IS NULL AND "Source"."sickleavehours" IS NULL)) OR NOT ("Target"."vacationhours" = "Source"."vacationhours" OR ("Target"."vacationhours" IS NULL AND "Source"."vacationhours" IS NULL))) THEN
  UPDATE SET
        "birthdate" = "Source"."birthdate",
        "businessentityid" = "Source"."businessentityid",
        "currentflag" = "Source"."currentflag",
        "gender" = "Source"."gender",
        "hiredate" = "Source"."hiredate",
        "jobtitle" = "Source"."jobtitle",
        "loginid" = "Source"."loginid",
        "maritalstatus" = "Source"."maritalstatus",
        "modifieddate" = "Source"."modifieddate",
        "nationalidnumber" = "Source"."nationalidnumber",
        "organizationnode" = "Source"."organizationnode",
        "rowguid" = "Source"."rowguid",
        "salariedflag" = "Source"."salariedflag",
        "sickleavehours" = "Source"."sickleavehours",
        "vacationhours" = "Source"."vacationhours"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "birthdate",
        "businessentityid",
        "currentflag",
        "gender",
        "hiredate",
        "jobtitle",
        "loginid",
        "maritalstatus",
        "modifieddate",
        "nationalidnumber",
        "organizationnode",
        "rowguid",
        "salariedflag",
        "sickleavehours",
        "vacationhours"
   ) 
  VALUES (
         "Source"."birthdate",
        "Source"."businessentityid",
        "Source"."currentflag",
        "Source"."gender",
        "Source"."hiredate",
        "Source"."jobtitle",
        "Source"."loginid",
        "Source"."maritalstatus",
        "Source"."modifieddate",
        "Source"."nationalidnumber",
        "Source"."organizationnode",
        "Source"."rowguid",
        "Source"."salariedflag",
        "Source"."sickleavehours",
        "Source"."vacationhours"
   )
 ;



END $$ LANGUAGE plpgsql;

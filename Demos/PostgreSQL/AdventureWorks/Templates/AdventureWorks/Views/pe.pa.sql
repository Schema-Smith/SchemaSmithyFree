CREATE OR REPLACE VIEW "pe"."pa" AS
 SELECT businessentityid AS id,
    businessentityid,
    passwordhash,
    passwordsalt,
    rowguid,
    modifieddate
   FROM person.password;
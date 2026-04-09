CREATE OR REPLACE VIEW "pe"."p" AS
 SELECT businessentityid AS id,
    businessentityid,
    persontype,
    namestyle,
    title,
    firstname,
    middlename,
    lastname,
    suffix,
    emailpromotion,
    additionalcontactinfo,
    demographics,
    rowguid,
    modifieddate
   FROM person.person;
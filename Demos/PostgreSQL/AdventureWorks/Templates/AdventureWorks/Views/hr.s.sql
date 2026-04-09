CREATE OR REPLACE VIEW "hr"."s" AS
 SELECT shiftid AS id,
    shiftid,
    name,
    starttime,
    endtime,
    modifieddate
   FROM humanresources.shift;
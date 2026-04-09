CREATE OR REPLACE VIEW "pr"."pmi" AS
 SELECT productmodelid,
    illustrationid,
    modifieddate
   FROM production.productmodelillustration;
CREATE OR REPLACE VIEW "pr"."i" AS
 SELECT illustrationid AS id,
    illustrationid,
    diagram,
    modifieddate
   FROM production.illustration;
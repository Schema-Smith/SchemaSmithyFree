CREATE OR REPLACE VIEW "pr"."bom" AS
 SELECT billofmaterialsid AS id,
    billofmaterialsid,
    productassemblyid,
    componentid,
    startdate,
    enddate,
    unitmeasurecode,
    bomlevel,
    perassemblyqty,
    modifieddate
   FROM production.billofmaterials;
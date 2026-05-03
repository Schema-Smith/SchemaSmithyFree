CREATE OR REPLACE VIEW "sales"."vsalespersonsalesbyfiscalyears" AS
 SELECT "SalesPersonID",
    "FullName",
    "JobTitle",
    "SalesTerritory",
    "2012",
    "2013",
    "2014"
   FROM crosstab('SELECT
    SalesPersonID
    ,FullName
    ,JobTitle
    ,SalesTerritory
    ,FiscalYear
    ,SalesTotal
FROM Sales.vSalesPersonSalesByFiscalYearsData
ORDER BY 2,4'::text, 'SELECT unnest(''{2012,2013,2014}''::text[])'::text) salestotal("SalesPersonID" integer, "FullName" text, "JobTitle" text, "SalesTerritory" text, "2012" numeric(12,4), "2013" numeric(12,4), "2014" numeric(12,4));
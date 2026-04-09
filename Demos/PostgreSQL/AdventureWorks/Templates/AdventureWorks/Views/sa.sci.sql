CREATE OR REPLACE VIEW "sa"."sci" AS
 SELECT shoppingcartitemid AS id,
    shoppingcartitemid,
    shoppingcartid,
    quantity,
    productid,
    datecreated,
    modifieddate
   FROM sales.shoppingcartitem;
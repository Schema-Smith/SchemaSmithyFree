CREATE OR REPLACE VIEW "pr"."pr" AS
 SELECT productreviewid AS id,
    productreviewid,
    productid,
    reviewername,
    reviewdate,
    emailaddress,
    rating,
    comments,
    modifieddate
   FROM production.productreview;
CREATE OR REPLACE VIEW "pr"."pp" AS
 SELECT productphotoid AS id,
    productphotoid,
    thumbnailphoto,
    thumbnailphotofilename,
    largephoto,
    largephotofilename,
    modifieddate
   FROM production.productphoto;
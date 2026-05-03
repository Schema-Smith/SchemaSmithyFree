CREATE OR REPLACE VIEW "pr"."d" AS
 SELECT title,
    owner,
    folderflag,
    filename,
    fileextension,
    revision,
    changenumber,
    status,
    documentsummary,
    document,
    rowguid,
    modifieddate,
    documentnode
   FROM production.document;
SET @json_data = '{{Production_Document.tabledata}}';

INSERT INTO `adventureworks`.`Production_Document` (`ChangeNumber`, `Document`, `DocumentNode`, `DocumentSummary`, `FileExtension`, `FileName`, `FolderFlag`, `ModifiedDate`, `Owner`, `Revision`, `rowguid`, `Status`, `Title`)
SELECT `ChangeNumber`, FROM_BASE64(`Document`), `DocumentNode`, `DocumentSummary`, `FileExtension`, `FileName`, `FolderFlag`, `ModifiedDate`, `Owner`, `Revision`, `rowguid`, `Status`, `Title`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ChangeNumber` INT PATH '$.ChangeNumber',
    `Document` TEXT PATH '$.Document',
    `DocumentNode` VARCHAR(255) PATH '$.DocumentNode',
    `DocumentSummary` TEXT PATH '$.DocumentSummary',
    `FileExtension` VARCHAR(8) PATH '$.FileExtension',
    `FileName` VARCHAR(400) PATH '$.FileName',
    `FolderFlag` TINYINT PATH '$.FolderFlag',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Owner` INT PATH '$.Owner',
    `Revision` CHAR(5) PATH '$.Revision',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `Status` TINYINT PATH '$.Status',
    `Title` VARCHAR(50) PATH '$.Title'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ChangeNumber` = VALUES(`ChangeNumber`),
  `Document` = VALUES(`Document`),
  `DocumentSummary` = VALUES(`DocumentSummary`),
  `FileExtension` = VALUES(`FileExtension`),
  `FileName` = VALUES(`FileName`),
  `FolderFlag` = VALUES(`FolderFlag`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Owner` = VALUES(`Owner`),
  `Revision` = VALUES(`Revision`),
  `rowguid` = VALUES(`rowguid`),
  `Status` = VALUES(`Status`),
  `Title` = VALUES(`Title`);

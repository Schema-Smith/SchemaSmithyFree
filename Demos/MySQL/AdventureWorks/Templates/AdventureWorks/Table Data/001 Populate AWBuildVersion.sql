SET @json_data = '{{AWBuildVersion.tabledata}}';

INSERT INTO `adventureworks`.`AWBuildVersion` (`Database Version`, `ModifiedDate`, `SystemInformationID`, `VersionDate`)
SELECT `Database Version`, `ModifiedDate`, `SystemInformationID`, `VersionDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Database Version` VARCHAR(25) PATH '$."Database Version"',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `SystemInformationID` TINYINT PATH '$.SystemInformationID',
    `VersionDate` DATETIME PATH '$.VersionDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Database Version` = VALUES(`Database Version`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `VersionDate` = VALUES(`VersionDate`);

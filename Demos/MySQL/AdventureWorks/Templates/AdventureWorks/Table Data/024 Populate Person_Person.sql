SET @json_data = '{{Person_Person.tabledata}}';

INSERT INTO `adventureworks`.`Person_Person` (`AdditionalContactInfo`, `BusinessEntityID`, `Demographics`, `EmailPromotion`, `FirstName`, `LastName`, `MiddleName`, `ModifiedDate`, `NameStyle`, `PersonType`, `rowguid`, `Suffix`, `Title`)
SELECT `AdditionalContactInfo`, `BusinessEntityID`, `Demographics`, `EmailPromotion`, `FirstName`, `LastName`, `MiddleName`, `ModifiedDate`, `NameStyle`, `PersonType`, `rowguid`, `Suffix`, `Title`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AdditionalContactInfo` TEXT PATH '$.AdditionalContactInfo',
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `Demographics` TEXT PATH '$.Demographics',
    `EmailPromotion` INT PATH '$.EmailPromotion',
    `FirstName` VARCHAR(50) PATH '$.FirstName',
    `LastName` VARCHAR(50) PATH '$.LastName',
    `MiddleName` VARCHAR(50) PATH '$.MiddleName',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `NameStyle` TINYINT PATH '$.NameStyle',
    `PersonType` CHAR(2) PATH '$.PersonType',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `Suffix` VARCHAR(10) PATH '$.Suffix',
    `Title` VARCHAR(8) PATH '$.Title'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `AdditionalContactInfo` = VALUES(`AdditionalContactInfo`),
  `Demographics` = VALUES(`Demographics`),
  `EmailPromotion` = VALUES(`EmailPromotion`),
  `FirstName` = VALUES(`FirstName`),
  `LastName` = VALUES(`LastName`),
  `MiddleName` = VALUES(`MiddleName`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `NameStyle` = VALUES(`NameStyle`),
  `PersonType` = VALUES(`PersonType`),
  `rowguid` = VALUES(`rowguid`),
  `Suffix` = VALUES(`Suffix`),
  `Title` = VALUES(`Title`);

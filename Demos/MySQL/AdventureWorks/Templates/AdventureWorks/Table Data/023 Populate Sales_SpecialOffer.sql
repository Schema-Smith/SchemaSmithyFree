SET @json_data = '{{Sales_SpecialOffer.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SpecialOffer` (`Category`, `Description`, `DiscountPct`, `EndDate`, `MaxQty`, `MinQty`, `ModifiedDate`, `rowguid`, `SpecialOfferID`, `StartDate`, `Type`)
SELECT `Category`, `Description`, `DiscountPct`, `EndDate`, `MaxQty`, `MinQty`, `ModifiedDate`, `rowguid`, `SpecialOfferID`, `StartDate`, `Type`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Category` VARCHAR(50) PATH '$.Category',
    `Description` VARCHAR(255) PATH '$.Description',
    `DiscountPct` DECIMAL(10,4) PATH '$.DiscountPct',
    `EndDate` DATETIME PATH '$.EndDate',
    `MaxQty` INT PATH '$.MaxQty',
    `MinQty` INT PATH '$.MinQty',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SpecialOfferID` INT PATH '$.SpecialOfferID',
    `StartDate` DATETIME PATH '$.StartDate',
    `Type` VARCHAR(50) PATH '$.Type'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Category` = VALUES(`Category`),
  `Description` = VALUES(`Description`),
  `DiscountPct` = VALUES(`DiscountPct`),
  `EndDate` = VALUES(`EndDate`),
  `MaxQty` = VALUES(`MaxQty`),
  `MinQty` = VALUES(`MinQty`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`),
  `StartDate` = VALUES(`StartDate`),
  `Type` = VALUES(`Type`);

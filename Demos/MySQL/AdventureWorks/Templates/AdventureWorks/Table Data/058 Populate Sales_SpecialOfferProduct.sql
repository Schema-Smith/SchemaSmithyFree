SET @json_data = '{{Sales_SpecialOfferProduct.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SpecialOfferProduct` (`ModifiedDate`, `ProductID`, `rowguid`, `SpecialOfferID`)
SELECT `ModifiedDate`, `ProductID`, `rowguid`, `SpecialOfferID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SpecialOfferID` INT PATH '$.SpecialOfferID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`);

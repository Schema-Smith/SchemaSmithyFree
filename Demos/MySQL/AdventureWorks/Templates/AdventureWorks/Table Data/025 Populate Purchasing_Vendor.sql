SET @json_data = '{{Purchasing_Vendor.tabledata}}';

INSERT INTO `adventureworks`.`Purchasing_Vendor` (`AccountNumber`, `ActiveFlag`, `BusinessEntityID`, `CreditRating`, `ModifiedDate`, `Name`, `PreferredVendorStatus`, `PurchasingWebServiceURL`)
SELECT `AccountNumber`, `ActiveFlag`, `BusinessEntityID`, `CreditRating`, `ModifiedDate`, `Name`, `PreferredVendorStatus`, `PurchasingWebServiceURL`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AccountNumber` VARCHAR(15) PATH '$.AccountNumber',
    `ActiveFlag` TINYINT PATH '$.ActiveFlag',
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `CreditRating` TINYINT PATH '$.CreditRating',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `PreferredVendorStatus` TINYINT PATH '$.PreferredVendorStatus',
    `PurchasingWebServiceURL` VARCHAR(1024) PATH '$.PurchasingWebServiceURL'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `AccountNumber` = VALUES(`AccountNumber`),
  `ActiveFlag` = VALUES(`ActiveFlag`),
  `CreditRating` = VALUES(`CreditRating`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `PreferredVendorStatus` = VALUES(`PreferredVendorStatus`),
  `PurchasingWebServiceURL` = VALUES(`PurchasingWebServiceURL`);

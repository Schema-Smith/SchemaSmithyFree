SET @json_data = '{{Purchasing_PurchaseOrderHeader.tabledata}}';

INSERT INTO `adventureworks`.`Purchasing_PurchaseOrderHeader` (`EmployeeID`, `Freight`, `ModifiedDate`, `OrderDate`, `PurchaseOrderID`, `RevisionNumber`, `ShipDate`, `ShipMethodID`, `Status`, `SubTotal`, `TaxAmt`, `VendorID`)
SELECT `EmployeeID`, `Freight`, `ModifiedDate`, `OrderDate`, `PurchaseOrderID`, `RevisionNumber`, `ShipDate`, `ShipMethodID`, `Status`, `SubTotal`, `TaxAmt`, `VendorID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `EmployeeID` INT PATH '$.EmployeeID',
    `Freight` DECIMAL(19,4) PATH '$.Freight',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `OrderDate` DATETIME PATH '$.OrderDate',
    `PurchaseOrderID` INT PATH '$.PurchaseOrderID',
    `RevisionNumber` TINYINT PATH '$.RevisionNumber',
    `ShipDate` DATETIME PATH '$.ShipDate',
    `ShipMethodID` INT PATH '$.ShipMethodID',
    `Status` TINYINT PATH '$.Status',
    `SubTotal` DECIMAL(19,4) PATH '$.SubTotal',
    `TaxAmt` DECIMAL(19,4) PATH '$.TaxAmt',
    `VendorID` INT PATH '$.VendorID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `EmployeeID` = VALUES(`EmployeeID`),
  `Freight` = VALUES(`Freight`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `OrderDate` = VALUES(`OrderDate`),
  `RevisionNumber` = VALUES(`RevisionNumber`),
  `ShipDate` = VALUES(`ShipDate`),
  `ShipMethodID` = VALUES(`ShipMethodID`),
  `Status` = VALUES(`Status`),
  `SubTotal` = VALUES(`SubTotal`),
  `TaxAmt` = VALUES(`TaxAmt`),
  `VendorID` = VALUES(`VendorID`);

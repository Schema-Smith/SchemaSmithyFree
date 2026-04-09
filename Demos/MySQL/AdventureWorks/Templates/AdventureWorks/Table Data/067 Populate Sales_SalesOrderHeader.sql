SET @json_data = '{{Sales_SalesOrderHeader.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesOrderHeader` (`AccountNumber`, `BillToAddressID`, `Comment`, `CreditCardApprovalCode`, `CreditCardID`, `CurrencyRateID`, `CustomerID`, `DueDate`, `Freight`, `ModifiedDate`, `OnlineOrderFlag`, `OrderDate`, `PurchaseOrderNumber`, `RevisionNumber`, `rowguid`, `SalesOrderID`, `SalesPersonID`, `ShipDate`, `ShipMethodID`, `ShipToAddressID`, `Status`, `SubTotal`, `TaxAmt`, `TerritoryID`)
SELECT `AccountNumber`, `BillToAddressID`, `Comment`, `CreditCardApprovalCode`, `CreditCardID`, `CurrencyRateID`, `CustomerID`, `DueDate`, `Freight`, `ModifiedDate`, `OnlineOrderFlag`, `OrderDate`, `PurchaseOrderNumber`, `RevisionNumber`, `rowguid`, `SalesOrderID`, `SalesPersonID`, `ShipDate`, `ShipMethodID`, `ShipToAddressID`, `Status`, `SubTotal`, `TaxAmt`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AccountNumber` VARCHAR(15) PATH '$.AccountNumber',
    `BillToAddressID` INT PATH '$.BillToAddressID',
    `Comment` VARCHAR(128) PATH '$.Comment',
    `CreditCardApprovalCode` VARCHAR(15) PATH '$.CreditCardApprovalCode',
    `CreditCardID` INT PATH '$.CreditCardID',
    `CurrencyRateID` INT PATH '$.CurrencyRateID',
    `CustomerID` INT PATH '$.CustomerID',
    `DueDate` DATETIME PATH '$.DueDate',
    `Freight` DECIMAL(19,4) PATH '$.Freight',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `OnlineOrderFlag` TINYINT PATH '$.OnlineOrderFlag',
    `OrderDate` DATETIME PATH '$.OrderDate',
    `PurchaseOrderNumber` VARCHAR(25) PATH '$.PurchaseOrderNumber',
    `RevisionNumber` TINYINT PATH '$.RevisionNumber',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalesOrderID` INT PATH '$.SalesOrderID',
    `SalesPersonID` INT PATH '$.SalesPersonID',
    `ShipDate` DATETIME PATH '$.ShipDate',
    `ShipMethodID` INT PATH '$.ShipMethodID',
    `ShipToAddressID` INT PATH '$.ShipToAddressID',
    `Status` TINYINT PATH '$.Status',
    `SubTotal` DECIMAL(19,4) PATH '$.SubTotal',
    `TaxAmt` DECIMAL(19,4) PATH '$.TaxAmt',
    `TerritoryID` INT PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `AccountNumber` = VALUES(`AccountNumber`),
  `BillToAddressID` = VALUES(`BillToAddressID`),
  `Comment` = VALUES(`Comment`),
  `CreditCardApprovalCode` = VALUES(`CreditCardApprovalCode`),
  `CreditCardID` = VALUES(`CreditCardID`),
  `CurrencyRateID` = VALUES(`CurrencyRateID`),
  `CustomerID` = VALUES(`CustomerID`),
  `DueDate` = VALUES(`DueDate`),
  `Freight` = VALUES(`Freight`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `OnlineOrderFlag` = VALUES(`OnlineOrderFlag`),
  `OrderDate` = VALUES(`OrderDate`),
  `PurchaseOrderNumber` = VALUES(`PurchaseOrderNumber`),
  `RevisionNumber` = VALUES(`RevisionNumber`),
  `rowguid` = VALUES(`rowguid`),
  `SalesPersonID` = VALUES(`SalesPersonID`),
  `ShipDate` = VALUES(`ShipDate`),
  `ShipMethodID` = VALUES(`ShipMethodID`),
  `ShipToAddressID` = VALUES(`ShipToAddressID`),
  `Status` = VALUES(`Status`),
  `SubTotal` = VALUES(`SubTotal`),
  `TaxAmt` = VALUES(`TaxAmt`),
  `TerritoryID` = VALUES(`TerritoryID`);

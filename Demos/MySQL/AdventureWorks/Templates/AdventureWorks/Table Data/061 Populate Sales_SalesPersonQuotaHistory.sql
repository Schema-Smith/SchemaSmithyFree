SET @json_data = '{{Sales_SalesPersonQuotaHistory.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesPersonQuotaHistory` (`BusinessEntityID`, `ModifiedDate`, `QuotaDate`, `rowguid`, `SalesQuota`)
SELECT `BusinessEntityID`, `ModifiedDate`, `QuotaDate`, `rowguid`, `SalesQuota`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `QuotaDate` DATETIME PATH '$.QuotaDate',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalesQuota` DECIMAL(19,4) PATH '$.SalesQuota'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`),
  `SalesQuota` = VALUES(`SalesQuota`);

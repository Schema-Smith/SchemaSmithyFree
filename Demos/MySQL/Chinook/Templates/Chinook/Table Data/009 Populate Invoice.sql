SET @json_data = '{{Invoice.tabledata}}';

INSERT INTO `Chinook`.`Invoice` (`InvoiceId`, `CustomerId`, `InvoiceDate`, `BillingAddress`, `BillingCity`, `BillingState`, `BillingCountry`, `BillingPostalCode`, `Total`)
SELECT `InvoiceId`, `CustomerId`, `InvoiceDate`, `BillingAddress`, `BillingCity`, `BillingState`, `BillingCountry`, `BillingPostalCode`, `Total`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `InvoiceId` INT PATH '$.InvoiceId',
    `CustomerId` INT PATH '$.CustomerId',
    `InvoiceDate` DATETIME PATH '$.InvoiceDate',
    `BillingAddress` VARCHAR(70) PATH '$.BillingAddress',
    `BillingCity` VARCHAR(40) PATH '$.BillingCity',
    `BillingState` VARCHAR(40) PATH '$.BillingState',
    `BillingCountry` VARCHAR(40) PATH '$.BillingCountry',
    `BillingPostalCode` VARCHAR(10) PATH '$.BillingPostalCode',
    `Total` DECIMAL(10,2) PATH '$.Total'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CustomerId` = VALUES(`CustomerId`),
  `InvoiceDate` = VALUES(`InvoiceDate`),
  `BillingAddress` = VALUES(`BillingAddress`),
  `BillingCity` = VALUES(`BillingCity`),
  `BillingState` = VALUES(`BillingState`),
  `BillingCountry` = VALUES(`BillingCountry`),
  `BillingPostalCode` = VALUES(`BillingPostalCode`),
  `Total` = VALUES(`Total`);

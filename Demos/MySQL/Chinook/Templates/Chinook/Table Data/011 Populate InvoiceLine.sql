SET @json_data = '{{InvoiceLine.tabledata}}';

INSERT INTO `Chinook`.`InvoiceLine` (`InvoiceLineId`, `InvoiceId`, `TrackId`, `UnitPrice`, `Quantity`)
SELECT `InvoiceLineId`, `InvoiceId`, `TrackId`, `UnitPrice`, `Quantity`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `InvoiceLineId` INT PATH '$.InvoiceLineId',
    `InvoiceId` INT PATH '$.InvoiceId',
    `TrackId` INT PATH '$.TrackId',
    `UnitPrice` DECIMAL(10,2) PATH '$.UnitPrice',
    `Quantity` INT PATH '$.Quantity'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `InvoiceId` = VALUES(`InvoiceId`),
  `TrackId` = VALUES(`TrackId`),
  `UnitPrice` = VALUES(`UnitPrice`),
  `Quantity` = VALUES(`Quantity`);

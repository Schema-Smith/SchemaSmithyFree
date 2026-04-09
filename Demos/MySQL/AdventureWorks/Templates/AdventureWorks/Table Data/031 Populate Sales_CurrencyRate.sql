SET @json_data = '{{Sales_CurrencyRate.tabledata}}';

INSERT INTO `adventureworks`.`Sales_CurrencyRate` (`AverageRate`, `CurrencyRateDate`, `CurrencyRateID`, `EndOfDayRate`, `FromCurrencyCode`, `ModifiedDate`, `ToCurrencyCode`)
SELECT `AverageRate`, `CurrencyRateDate`, `CurrencyRateID`, `EndOfDayRate`, `FromCurrencyCode`, `ModifiedDate`, `ToCurrencyCode`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AverageRate` DECIMAL(19,4) PATH '$.AverageRate',
    `CurrencyRateDate` DATETIME PATH '$.CurrencyRateDate',
    `CurrencyRateID` INT PATH '$.CurrencyRateID',
    `EndOfDayRate` DECIMAL(19,4) PATH '$.EndOfDayRate',
    `FromCurrencyCode` CHAR(3) PATH '$.FromCurrencyCode',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ToCurrencyCode` CHAR(3) PATH '$.ToCurrencyCode'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `AverageRate` = VALUES(`AverageRate`),
  `CurrencyRateDate` = VALUES(`CurrencyRateDate`),
  `EndOfDayRate` = VALUES(`EndOfDayRate`),
  `FromCurrencyCode` = VALUES(`FromCurrencyCode`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `ToCurrencyCode` = VALUES(`ToCurrencyCode`);

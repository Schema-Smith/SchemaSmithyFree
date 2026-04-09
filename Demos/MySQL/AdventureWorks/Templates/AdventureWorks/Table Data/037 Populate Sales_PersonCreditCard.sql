SET @json_data = '{{Sales_PersonCreditCard.tabledata}}';

INSERT INTO `adventureworks`.`Sales_PersonCreditCard` (`BusinessEntityID`, `CreditCardID`, `ModifiedDate`)
SELECT `BusinessEntityID`, `CreditCardID`, `ModifiedDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `CreditCardID` INT PATH '$.CreditCardID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`);

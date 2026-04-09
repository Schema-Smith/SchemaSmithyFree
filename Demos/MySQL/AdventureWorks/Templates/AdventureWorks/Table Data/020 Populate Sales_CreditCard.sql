SET @json_data = '{{Sales_CreditCard.tabledata}}';

INSERT INTO `adventureworks`.`Sales_CreditCard` (`CardNumber`, `CardType`, `CreditCardID`, `ExpMonth`, `ExpYear`, `ModifiedDate`)
SELECT `CardNumber`, `CardType`, `CreditCardID`, `ExpMonth`, `ExpYear`, `ModifiedDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CardNumber` VARCHAR(25) PATH '$.CardNumber',
    `CardType` VARCHAR(50) PATH '$.CardType',
    `CreditCardID` INT PATH '$.CreditCardID',
    `ExpMonth` TINYINT PATH '$.ExpMonth',
    `ExpYear` SMALLINT PATH '$.ExpYear',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CardNumber` = VALUES(`CardNumber`),
  `CardType` = VALUES(`CardType`),
  `ExpMonth` = VALUES(`ExpMonth`),
  `ExpYear` = VALUES(`ExpYear`),
  `ModifiedDate` = VALUES(`ModifiedDate`);

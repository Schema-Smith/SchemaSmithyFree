SET @json_data = '{{Sales_ShoppingCartItem.tabledata}}';

INSERT INTO `adventureworks`.`Sales_ShoppingCartItem` (`DateCreated`, `ModifiedDate`, `ProductID`, `Quantity`, `ShoppingCartID`, `ShoppingCartItemID`)
SELECT `DateCreated`, `ModifiedDate`, `ProductID`, `Quantity`, `ShoppingCartID`, `ShoppingCartItemID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `DateCreated` DATETIME PATH '$.DateCreated',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID',
    `Quantity` INT PATH '$.Quantity',
    `ShoppingCartID` VARCHAR(50) PATH '$.ShoppingCartID',
    `ShoppingCartItemID` INT PATH '$.ShoppingCartItemID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `DateCreated` = VALUES(`DateCreated`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `ProductID` = VALUES(`ProductID`),
  `Quantity` = VALUES(`Quantity`),
  `ShoppingCartID` = VALUES(`ShoppingCartID`);

SET @json_data = '{{Production_ProductReview.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductReview` (`Comments`, `EmailAddress`, `ModifiedDate`, `ProductID`, `ProductReviewID`, `Rating`, `ReviewDate`, `ReviewerName`)
SELECT `Comments`, `EmailAddress`, `ModifiedDate`, `ProductID`, `ProductReviewID`, `Rating`, `ReviewDate`, `ReviewerName`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Comments` VARCHAR(3850) PATH '$.Comments',
    `EmailAddress` VARCHAR(50) PATH '$.EmailAddress',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID',
    `ProductReviewID` INT PATH '$.ProductReviewID',
    `Rating` INT PATH '$.Rating',
    `ReviewDate` DATETIME PATH '$.ReviewDate',
    `ReviewerName` VARCHAR(50) PATH '$.ReviewerName'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Comments` = VALUES(`Comments`),
  `EmailAddress` = VALUES(`EmailAddress`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `ProductID` = VALUES(`ProductID`),
  `Rating` = VALUES(`Rating`),
  `ReviewDate` = VALUES(`ReviewDate`),
  `ReviewerName` = VALUES(`ReviewerName`);

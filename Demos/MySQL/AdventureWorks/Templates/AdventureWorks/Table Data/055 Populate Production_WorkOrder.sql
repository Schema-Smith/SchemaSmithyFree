SET @json_data = '{{Production_WorkOrder.tabledata}}';

INSERT INTO `adventureworks`.`Production_WorkOrder` (`DueDate`, `EndDate`, `ModifiedDate`, `OrderQty`, `ProductID`, `ScrappedQty`, `ScrapReasonID`, `StartDate`, `WorkOrderID`)
SELECT `DueDate`, `EndDate`, `ModifiedDate`, `OrderQty`, `ProductID`, `ScrappedQty`, `ScrapReasonID`, `StartDate`, `WorkOrderID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `DueDate` DATETIME PATH '$.DueDate',
    `EndDate` DATETIME PATH '$.EndDate',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `OrderQty` INT PATH '$.OrderQty',
    `ProductID` INT PATH '$.ProductID',
    `ScrappedQty` SMALLINT PATH '$.ScrappedQty',
    `ScrapReasonID` SMALLINT PATH '$.ScrapReasonID',
    `StartDate` DATETIME PATH '$.StartDate',
    `WorkOrderID` INT PATH '$.WorkOrderID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `DueDate` = VALUES(`DueDate`),
  `EndDate` = VALUES(`EndDate`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `OrderQty` = VALUES(`OrderQty`),
  `ProductID` = VALUES(`ProductID`),
  `ScrappedQty` = VALUES(`ScrappedQty`),
  `ScrapReasonID` = VALUES(`ScrapReasonID`),
  `StartDate` = VALUES(`StartDate`);

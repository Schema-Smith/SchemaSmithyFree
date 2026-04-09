SET @json_data = '{{Production_WorkOrderRouting.tabledata}}';

INSERT INTO `adventureworks`.`Production_WorkOrderRouting` (`ActualCost`, `ActualEndDate`, `ActualResourceHrs`, `ActualStartDate`, `LocationID`, `ModifiedDate`, `OperationSequence`, `PlannedCost`, `ProductID`, `ScheduledEndDate`, `ScheduledStartDate`, `WorkOrderID`)
SELECT `ActualCost`, `ActualEndDate`, `ActualResourceHrs`, `ActualStartDate`, `LocationID`, `ModifiedDate`, `OperationSequence`, `PlannedCost`, `ProductID`, `ScheduledEndDate`, `ScheduledStartDate`, `WorkOrderID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ActualCost` DECIMAL(19,4) PATH '$.ActualCost',
    `ActualEndDate` DATETIME PATH '$.ActualEndDate',
    `ActualResourceHrs` DECIMAL(9,4) PATH '$.ActualResourceHrs',
    `ActualStartDate` DATETIME PATH '$.ActualStartDate',
    `LocationID` SMALLINT PATH '$.LocationID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `OperationSequence` SMALLINT PATH '$.OperationSequence',
    `PlannedCost` DECIMAL(19,4) PATH '$.PlannedCost',
    `ProductID` INT PATH '$.ProductID',
    `ScheduledEndDate` DATETIME PATH '$.ScheduledEndDate',
    `ScheduledStartDate` DATETIME PATH '$.ScheduledStartDate',
    `WorkOrderID` INT PATH '$.WorkOrderID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ActualCost` = VALUES(`ActualCost`),
  `ActualEndDate` = VALUES(`ActualEndDate`),
  `ActualResourceHrs` = VALUES(`ActualResourceHrs`),
  `ActualStartDate` = VALUES(`ActualStartDate`),
  `LocationID` = VALUES(`LocationID`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `PlannedCost` = VALUES(`PlannedCost`),
  `ScheduledEndDate` = VALUES(`ScheduledEndDate`),
  `ScheduledStartDate` = VALUES(`ScheduledStartDate`);

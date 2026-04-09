SET @json_data = '{{HumanResources_Shift.tabledata}}';

INSERT INTO `adventureworks`.`HumanResources_Shift` (`EndTime`, `ModifiedDate`, `Name`, `ShiftID`, `StartTime`)
SELECT `EndTime`, `ModifiedDate`, `Name`, `ShiftID`, `StartTime`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `EndTime` TIME PATH '$.EndTime',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `ShiftID` TINYINT PATH '$.ShiftID',
    `StartTime` TIME PATH '$.StartTime'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `EndTime` = VALUES(`EndTime`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `StartTime` = VALUES(`StartTime`);

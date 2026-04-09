SET @json_data = '{{HumanResources_JobCandidate.tabledata}}';

INSERT INTO `adventureworks`.`HumanResources_JobCandidate` (`BusinessEntityID`, `JobCandidateID`, `ModifiedDate`, `Resume`)
SELECT `BusinessEntityID`, `JobCandidateID`, `ModifiedDate`, `Resume`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `JobCandidateID` INT PATH '$.JobCandidateID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Resume` TEXT PATH '$.Resume'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `BusinessEntityID` = VALUES(`BusinessEntityID`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Resume` = VALUES(`Resume`);

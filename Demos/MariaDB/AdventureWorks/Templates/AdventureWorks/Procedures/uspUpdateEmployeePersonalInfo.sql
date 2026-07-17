DROP PROCEDURE IF EXISTS `uspUpdateEmployeePersonalInfo`;
DELIMITER //
CREATE PROCEDURE `uspUpdateEmployeePersonalInfo` (IN p_BusinessEntityID int,IN p_NationalIDNumber varchar(15),IN p_BirthDate datetime,IN p_MaritalStatus char(1),IN p_Gender char(1))
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN
    DECLARE v_ErrorLogID INT;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        CALL `uspLogError`(v_ErrorLogID);
    END;

    UPDATE `HumanResources_Employee`
    SET `NationalIDNumber` = p_NationalIDNumber
        ,`BirthDate` = p_BirthDate
        ,`MaritalStatus` = p_MaritalStatus
        ,`Gender` = p_Gender
    WHERE `BusinessEntityID` = p_BusinessEntityID;
END //
DELIMITER ;
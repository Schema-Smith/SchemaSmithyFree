DROP TRIGGER IF EXISTS `HumanResources_Employee_dEmployee`;
DELIMITER //
CREATE TRIGGER `HumanResources_Employee_dEmployee`
  BEFORE DELETE
  ON `HumanResources_Employee` 
  FOR EACH ROW 
BEGIN
BEGIN
BEGIN
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Employees cannot be deleted. They can only be marked as not current.';
END;
END;
END //
DELIMITER ;
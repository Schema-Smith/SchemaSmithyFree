DROP PROCEDURE IF EXISTS `Employee Sales by Country`;
DELIMITER //
CREATE PROCEDURE `Employee Sales by Country` (IN p_Beginning_Date datetime,IN p_Ending_Date datetime)
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN

SELECT `Employees`.`Country`, `Employees`.`LastName`, `Employees`.`FirstName`, `Orders`.`ShippedDate`, `Orders`.`OrderID`, `Order Subtotals`.`Subtotal` AS `SaleAmount`
FROM `Employees` INNER JOIN
	(`Orders` INNER JOIN `Order Subtotals` ON `Orders`.`OrderID` = `Order Subtotals`.`OrderID`)
	ON `Employees`.`EmployeeID` = `Orders`.`EmployeeID`
WHERE `Orders`.`ShippedDate` BETWEEN p_Beginning_Date AND p_Ending_Date;

END //
DELIMITER ;
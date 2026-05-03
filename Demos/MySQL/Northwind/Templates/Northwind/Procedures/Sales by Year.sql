DROP PROCEDURE IF EXISTS `Sales by Year`;
DELIMITER //
CREATE PROCEDURE `Sales by Year` (IN p_Beginning_Date datetime,IN p_Ending_Date datetime)
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN

SELECT `Orders`.`ShippedDate`, `Orders`.`OrderID`, `Order Subtotals`.`Subtotal`, YEAR(`ShippedDate`) AS `Year`
FROM `Orders` INNER JOIN `Order Subtotals` ON `Orders`.`OrderID` = `Order Subtotals`.`OrderID`
WHERE `Orders`.`ShippedDate` BETWEEN p_Beginning_Date AND p_Ending_Date;

END //
DELIMITER ;
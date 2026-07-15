DROP PROCEDURE IF EXISTS `CustOrdersOrders`;
DELIMITER //
CREATE PROCEDURE `CustOrdersOrders` (IN p_CustomerID char(5))
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN

SELECT `OrderID`,
	`OrderDate`,
	`RequiredDate`,
	`ShippedDate`
FROM `Orders`
WHERE `CustomerID` = p_CustomerID
ORDER BY `OrderID`;

END //
DELIMITER ;
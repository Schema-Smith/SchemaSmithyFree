DROP PROCEDURE IF EXISTS `CustOrdersDetail`;
DELIMITER //
CREATE PROCEDURE `CustOrdersDetail` (IN p_OrderID int)
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN

SELECT `ProductName`,
    ROUND(Od.`UnitPrice`, 2) AS `UnitPrice`,
    `Quantity`,
    CAST(`Discount` * 100 AS SIGNED) AS `Discount`,
    ROUND(`Quantity` * (1 - `Discount`) * Od.`UnitPrice`, 2) AS `ExtendedPrice`
FROM `Products` P, `Order Details` Od
WHERE Od.`ProductID` = P.`ProductID` AND Od.`OrderID` = p_OrderID;

END //
DELIMITER ;
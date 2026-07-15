DROP PROCEDURE IF EXISTS `CustOrderHist`;
DELIMITER //
CREATE PROCEDURE `CustOrderHist` (IN p_CustomerID char(5))
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN

SELECT `ProductName`, SUM(`Quantity`) AS `Total`
FROM `Products` P, `Order Details` OD, `Orders` O, `Customers` C
WHERE C.`CustomerID` = p_CustomerID
AND C.`CustomerID` = O.`CustomerID` AND O.`OrderID` = OD.`OrderID` AND OD.`ProductID` = P.`ProductID`
GROUP BY `ProductName`;

END //
DELIMITER ;
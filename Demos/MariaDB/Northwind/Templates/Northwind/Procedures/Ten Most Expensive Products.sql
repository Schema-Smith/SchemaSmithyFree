DROP PROCEDURE IF EXISTS `Ten Most Expensive Products`;
DELIMITER //
CREATE PROCEDURE `Ten Most Expensive Products` ()
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN

SELECT `Products`.`ProductName` AS `TenMostExpensiveProducts`, `Products`.`UnitPrice`
FROM `Products`
ORDER BY `Products`.`UnitPrice` DESC
LIMIT 10;

END //
DELIMITER ;
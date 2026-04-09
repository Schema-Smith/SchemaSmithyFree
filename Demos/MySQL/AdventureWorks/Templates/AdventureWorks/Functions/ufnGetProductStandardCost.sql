DROP FUNCTION IF EXISTS `ufnGetProductStandardCost`;
DELIMITER //
CREATE FUNCTION `ufnGetProductStandardCost` (p_ProductID int,p_OrderDate datetime)
  RETURNS decimal(19,4)
  LANGUAGE SQL
  NOT DETERMINISTIC
  READS SQL DATA
  SQL SECURITY DEFINER
BEGIN
    DECLARE StandardCost DECIMAL(19,4);

    SELECT pch.`StandardCost` INTO StandardCost
    FROM `Production_Product` p
        INNER JOIN `Production_ProductCostHistory` pch
        ON p.`ProductID` = pch.`ProductID`
            AND p.`ProductID` = p_ProductID
            AND p_OrderDate BETWEEN pch.`StartDate` AND COALESCE(pch.`EndDate`, CAST('9999-12-31' AS DATETIME));

    RETURN StandardCost;
END //
DELIMITER ;
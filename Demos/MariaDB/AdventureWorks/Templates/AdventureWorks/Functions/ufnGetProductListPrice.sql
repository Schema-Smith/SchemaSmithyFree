DROP FUNCTION IF EXISTS `ufnGetProductListPrice`;
DELIMITER //
CREATE FUNCTION `ufnGetProductListPrice` (p_ProductID int,p_OrderDate datetime)
  RETURNS decimal(19,4)
  LANGUAGE SQL
  NOT DETERMINISTIC
  READS SQL DATA
  SQL SECURITY DEFINER
BEGIN
    DECLARE ListPrice DECIMAL(19,4);

    SELECT plph.`ListPrice` INTO ListPrice
    FROM `Production_Product` p
        INNER JOIN `Production_ProductListPriceHistory` plph
        ON p.`ProductID` = plph.`ProductID`
            AND p.`ProductID` = p_ProductID
            AND p_OrderDate BETWEEN plph.`StartDate` AND COALESCE(plph.`EndDate`, CAST('9999-12-31' AS DATETIME));

    RETURN ListPrice;
END //
DELIMITER ;
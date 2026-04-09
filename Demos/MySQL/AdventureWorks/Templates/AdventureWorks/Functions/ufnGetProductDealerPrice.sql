DROP FUNCTION IF EXISTS `ufnGetProductDealerPrice`;
DELIMITER //
CREATE FUNCTION `ufnGetProductDealerPrice` (p_ProductID int,p_OrderDate datetime)
  RETURNS decimal(19,4)
  LANGUAGE SQL
  NOT DETERMINISTIC
  READS SQL DATA
  SQL SECURITY DEFINER
BEGIN
    DECLARE DealerPrice DECIMAL(19,4);
    DECLARE DealerDiscount DECIMAL(19,4) DEFAULT 0.60;

    SELECT plph.`ListPrice` * DealerDiscount INTO DealerPrice
    FROM `Production_Product` p
        INNER JOIN `Production_ProductListPriceHistory` plph
        ON p.`ProductID` = plph.`ProductID`
            AND p.`ProductID` = p_ProductID
            AND p_OrderDate BETWEEN plph.`StartDate` AND COALESCE(plph.`EndDate`, CAST('9999-12-31' AS DATETIME));

    RETURN DealerPrice;
END //
DELIMITER ;
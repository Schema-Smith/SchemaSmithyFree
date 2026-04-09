DROP FUNCTION IF EXISTS `ufnGetStock`;
DELIMITER //
CREATE FUNCTION `ufnGetStock` (p_ProductID int)
  RETURNS int
  LANGUAGE SQL
  NOT DETERMINISTIC
  READS SQL DATA
  SQL SECURITY DEFINER
BEGIN
    DECLARE ret INT;

    SELECT SUM(p.`Quantity`) INTO ret
    FROM `Production_ProductInventory` p
    WHERE p.`ProductID` = p_ProductID
        AND p.`LocationID` = 6;

    IF (ret IS NULL) THEN
        SET ret = 0;
    END IF;

    RETURN ret;
END //
DELIMITER ;
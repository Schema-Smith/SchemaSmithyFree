DROP FUNCTION IF EXISTS `ufnGetPurchaseOrderStatusText`;
DELIMITER //
CREATE FUNCTION `ufnGetPurchaseOrderStatusText` (p_Status tinyint unsigned)
  RETURNS varchar(15)
  LANGUAGE SQL
  DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN
    DECLARE ret VARCHAR(15);

    SET ret =
        CASE p_Status
            WHEN 1 THEN 'Pending'
            WHEN 2 THEN 'Approved'
            WHEN 3 THEN 'Rejected'
            WHEN 4 THEN 'Complete'
            ELSE '** Invalid **'
        END;

    RETURN ret;
END //
DELIMITER ;
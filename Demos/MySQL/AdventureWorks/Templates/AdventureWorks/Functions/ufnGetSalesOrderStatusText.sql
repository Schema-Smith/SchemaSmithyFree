DROP FUNCTION IF EXISTS `ufnGetSalesOrderStatusText`;
DELIMITER //
CREATE FUNCTION `ufnGetSalesOrderStatusText` (p_Status tinyint unsigned)
  RETURNS varchar(15)
  LANGUAGE SQL
  DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN
    DECLARE ret VARCHAR(15);

    SET ret =
        CASE p_Status
            WHEN 1 THEN 'In process'
            WHEN 2 THEN 'Approved'
            WHEN 3 THEN 'Backordered'
            WHEN 4 THEN 'Rejected'
            WHEN 5 THEN 'Shipped'
            WHEN 6 THEN 'Cancelled'
            ELSE '** Invalid **'
        END;

    RETURN ret;
END //
DELIMITER ;
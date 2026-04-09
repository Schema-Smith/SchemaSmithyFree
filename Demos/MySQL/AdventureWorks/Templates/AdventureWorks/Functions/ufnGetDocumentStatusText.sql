DROP FUNCTION IF EXISTS `ufnGetDocumentStatusText`;
DELIMITER //
CREATE FUNCTION `ufnGetDocumentStatusText` (p_Status tinyint unsigned)
  RETURNS varchar(16)
  LANGUAGE SQL
  DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN
    DECLARE ret VARCHAR(16);

    SET ret =
        CASE p_Status
            WHEN 1 THEN 'Pending approval'
            WHEN 2 THEN 'Approved'
            WHEN 3 THEN 'Obsolete'
            ELSE '** Invalid **'
        END;

    RETURN ret;
END //
DELIMITER ;
DROP PROCEDURE IF EXISTS `uspPrintError`;
DELIMITER //
CREATE PROCEDURE `uspPrintError` ()
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN
    DECLARE v_errno INT;
    DECLARE v_msg TEXT;
    DECLARE v_sqlstate CHAR(5);

    GET DIAGNOSTICS CONDITION 1
        v_errno = MYSQL_ERRNO,
        v_msg = MESSAGE_TEXT,
        v_sqlstate = RETURNED_SQLSTATE;

    SELECT CONCAT('Error ', v_errno,
                  ', SQLState ', v_sqlstate,
                  ', Message: ', v_msg) AS ErrorInfo;
END //
DELIMITER ;
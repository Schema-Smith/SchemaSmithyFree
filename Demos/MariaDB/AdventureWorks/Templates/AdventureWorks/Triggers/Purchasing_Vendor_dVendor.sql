DROP TRIGGER IF EXISTS `Purchasing_Vendor_dVendor`;
DELIMITER //
CREATE TRIGGER `Purchasing_Vendor_dVendor`
  BEFORE DELETE
  ON `Purchasing_Vendor` 
  FOR EACH ROW 
BEGIN
BEGIN
BEGIN
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Vendors cannot be deleted. They can only be marked as not active.';
END;
END;
END //
DELIMITER ;
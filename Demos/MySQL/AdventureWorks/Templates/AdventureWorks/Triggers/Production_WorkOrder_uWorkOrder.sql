DROP TRIGGER IF EXISTS `Production_WorkOrder_uWorkOrder`;
DELIMITER //
CREATE TRIGGER `Production_WorkOrder_uWorkOrder`
  AFTER UPDATE
  ON `Production_WorkOrder` 
  FOR EACH ROW 
BEGIN
BEGIN
BEGIN
    IF (NEW.`ProductID` <> OLD.`ProductID`) OR (NEW.`OrderQty` <> OLD.`OrderQty`) THEN
        INSERT INTO `Production_TransactionHistory`(
            `ProductID`
            ,`ReferenceOrderID`
            ,`TransactionType`
            ,`TransactionDate`
            ,`Quantity`)
        VALUES (
            NEW.`ProductID`
            ,NEW.`WorkOrderID`
            ,'W'
            ,NOW()
            ,NEW.`OrderQty`
        );
    END IF;
END;
END;
END //
DELIMITER ;
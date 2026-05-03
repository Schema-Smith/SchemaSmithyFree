DROP TRIGGER IF EXISTS `Production_WorkOrder_iWorkOrder`;
DELIMITER //
CREATE TRIGGER `Production_WorkOrder_iWorkOrder`
  AFTER INSERT
  ON `Production_WorkOrder` 
  FOR EACH ROW 
BEGIN
BEGIN
BEGIN
    INSERT INTO `Production_TransactionHistory`(
        `ProductID`
        ,`ReferenceOrderID`
        ,`TransactionType`
        ,`TransactionDate`
        ,`Quantity`
        ,`ActualCost`)
    VALUES (
        NEW.`ProductID`
        ,NEW.`WorkOrderID`
        ,'W'
        ,NOW()
        ,NEW.`OrderQty`
        ,0
    );
END;
END;
END //
DELIMITER ;
DROP VIEW IF EXISTS `Current Product List`;
CREATE VIEW `Current Product List` AS
select `Product_List`.`ProductID` AS `ProductID`,`Product_List`.`ProductName` AS `ProductName` from `northwind`.`Products` `Product_List` where (`Product_List`.`Discontinued` = 0)
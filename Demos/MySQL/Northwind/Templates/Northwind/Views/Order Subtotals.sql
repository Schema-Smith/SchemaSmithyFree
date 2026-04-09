DROP VIEW IF EXISTS `Order Subtotals`;
CREATE VIEW `Order Subtotals` AS
select `northwind`.`Order Details`.`OrderID` AS `OrderID`,sum(round(((`northwind`.`Order Details`.`UnitPrice` * `northwind`.`Order Details`.`Quantity`) * (1 - `northwind`.`Order Details`.`Discount`)),2)) AS `Subtotal` from `northwind`.`Order Details` group by `northwind`.`Order Details`.`OrderID`
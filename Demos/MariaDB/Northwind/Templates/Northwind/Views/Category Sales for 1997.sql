DROP VIEW IF EXISTS `Category Sales for 1997`;
CREATE VIEW `Category Sales for 1997` AS
select `northwind`.`Product Sales for 1997`.`CategoryName` AS `CategoryName`,sum(`northwind`.`Product Sales for 1997`.`ProductSales`) AS `CategorySales` from `northwind`.`Product Sales for 1997` group by `northwind`.`Product Sales for 1997`.`CategoryName`
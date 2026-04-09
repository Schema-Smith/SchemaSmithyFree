DROP VIEW IF EXISTS `Products Above Average Price`;
CREATE VIEW `Products Above Average Price` AS
select `northwind`.`Products`.`ProductName` AS `ProductName`,`northwind`.`Products`.`UnitPrice` AS `UnitPrice` from `northwind`.`Products` where (`northwind`.`Products`.`UnitPrice` > (select avg(`northwind`.`Products`.`UnitPrice`) from `northwind`.`Products`))
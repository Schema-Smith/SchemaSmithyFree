CREATE OR REPLACE VIEW `vw_ActiveProducts` AS
SELECT `ProductId`, `Name`, `Sku`, `UnitPrice` FROM `Product`;

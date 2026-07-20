-- Bootstrap the control database the MySQL demo quenches connect to. The Initialize
-- template in each product connects to TestMain and issues CREATE DATABASE for the
-- product DB, so TestMain only needs to exist and carry the helper-owned marker.
DROP DATABASE IF EXISTS `TestMain`;
CREATE DATABASE `TestMain` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE TABLE `TestMain`.`SchemaSmith_DemoProvisioned` (marker TINYINT NOT NULL);

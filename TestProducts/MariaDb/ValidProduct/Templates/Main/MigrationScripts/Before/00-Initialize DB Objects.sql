-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE TABLE IF NOT EXISTS `SchemaSmith_TestLog` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `Msg` VARCHAR(500) NOT NULL
);

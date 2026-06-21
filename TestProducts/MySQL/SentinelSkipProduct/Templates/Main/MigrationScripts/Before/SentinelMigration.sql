-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Create a marker table if this is the first run, insert a row to prove the
-- pre-sentinel work lands, then raise the sentinel to skip the migration.
CREATE TABLE IF NOT EXISTS `SentinelMarker` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `Marker` VARCHAR(100) NOT NULL
);

INSERT INTO `SentinelMarker` (`Marker`) VALUES ('sentinel-skip-test');

SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SCHEMASMITH: SHOULD NOT APPLY';

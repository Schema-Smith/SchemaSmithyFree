-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Status messages table for real-time progress monitoring during quench operations.
-- Procedures INSERT messages into this table, and a C# background thread polls for new entries.
-- Messages are scoped by SessionId (CONNECTION_ID()) so concurrent operations don't interfere.

CREATE TABLE IF NOT EXISTS `SchemaSmith_StatusMessages` (
    `Id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `SessionId` BIGINT NOT NULL,
    `Message` TEXT NOT NULL,
    `CreatedAt` DATETIME(3) DEFAULT NOW(3),
    INDEX `IX_SessionId` (`SessionId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

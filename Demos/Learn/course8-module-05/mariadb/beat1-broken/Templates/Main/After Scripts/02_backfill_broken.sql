-- Broken backfill: intentionally passes NULL for the required Email column; this script will fail.
INSERT INTO `Customer` (`CustomerId`, `Email`, `FullName`) VALUES (21, NULL, 'Ivan Bronze');

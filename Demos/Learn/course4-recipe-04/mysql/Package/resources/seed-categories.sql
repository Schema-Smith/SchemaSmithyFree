-- Reference data kept in its own file and embedded into a deploy script via the File token.
-- Idempotent: INSERT IGNORE skips categories that are already present.
INSERT IGNORE INTO Category (CategoryName)
VALUES ('Books'), ('Electronics'), ('Garden');

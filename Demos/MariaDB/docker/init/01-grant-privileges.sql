-- Grant TestUser full privileges for integration testing and demos
-- This runs once when the container is first initialized
-- This is a disposable test container — grant all privileges to avoid
-- chasing individual database name patterns as new tests are added.

GRANT ALL PRIVILEGES ON *.* TO 'TestUser'@'%' WITH GRANT OPTION;

FLUSH PRIVILEGES;

-- Create marker table to signal init script has completed
-- The healthcheck verifies this table exists before reporting healthy
CREATE TABLE IF NOT EXISTS `TestMain`.`_init_complete` (
    completed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
INSERT INTO `TestMain`.`_init_complete` VALUES (NOW());

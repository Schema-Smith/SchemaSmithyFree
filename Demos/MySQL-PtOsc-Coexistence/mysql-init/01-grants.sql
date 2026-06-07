-- Grant REPLICATION CLIENT to the demo user so pt-online-schema-change
-- can run SHOW replica STATUS at startup. MySQL 9 requires this; without
-- it, pt-osc aborts with a replication-status check failure.
GRANT REPLICATION CLIENT ON *.* TO 'demo_user'@'%';
FLUSH PRIVILEGES;

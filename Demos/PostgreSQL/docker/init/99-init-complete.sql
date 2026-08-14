-- Runs LAST in the official postgres entrypoint's init phase (the 99- prefix orders it after any other
-- /docker-entrypoint-initdb.d scripts). The demoserver healthcheck gates on this table existing, so it
-- cannot report healthy until the socket-only init server has finished and the real (TCP) server is up —
-- the same _init_complete sentinel pattern the MySQL and MariaDB demos use.
CREATE TABLE IF NOT EXISTS _init_complete (ok integer);
INSERT INTO _init_complete VALUES (1);

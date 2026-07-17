-- Course 7 setup: five empty tenant databases (MySQL).
-- In MySQL a schema and a database are the same thing; discovered by Course 7's
-- information_schema.schemata query on the 'fleet_tenant_' prefix.
CREATE DATABASE IF NOT EXISTS `fleet_tenant_001`;
CREATE DATABASE IF NOT EXISTS `fleet_tenant_002`;
CREATE DATABASE IF NOT EXISTS `fleet_tenant_003`;
CREATE DATABASE IF NOT EXISTS `fleet_tenant_004`;
CREATE DATABASE IF NOT EXISTS `fleet_tenant_005`;

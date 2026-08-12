-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_UnsupportedFeaturePolicy`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_UnsupportedFeaturePolicy`()
-- CHARACTER SET is explicit for the same reason as SchemaSmith_JsonScalarStr: an unqualified text return
-- inherits the database's collation, and this result is compared against 'warn'/'fail' literals carrying
-- the connection's, which raises "Illegal mix of collations" on a database that differs from the server
-- default.
RETURNS VARCHAR(4) CHARACTER SET utf8mb4
NOT DETERMINISTIC
NO SQL
BEGIN
  -- Policy for a model feature the DETECTED target version cannot support: 'warn' (default) emits the
  -- degraded form plus a 'downgraded' manifest row; 'fail' aborts. Set per-connection by DatabaseQuench
  -- from Target:UnsupportedFeaturePolicy into the @schemasmith_unsupported_policy session variable
  -- (mirroring the PG schemasmith.unsupported_policy GUC and the @schemasmith_version_override affordance).
  -- Any value other than an explicit 'fail' resolves to the safe 'warn' default.
  RETURN CASE WHEN LOWER(COALESCE(NULLIF(@schemasmith_unsupported_policy, ''), 'warn')) = 'fail'
              THEN 'fail' ELSE 'warn' END;
END //

DELIMITER ;

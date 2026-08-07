-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_UnsupportedFeaturePolicy`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_UnsupportedFeaturePolicy`()
RETURNS VARCHAR(4)
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

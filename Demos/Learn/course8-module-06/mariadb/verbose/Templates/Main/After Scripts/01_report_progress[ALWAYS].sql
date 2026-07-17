-- VerboseLogging demo. MySQL user scripts have no progress channel (no InfoMessage/Notice event);
-- a SELECT result set is not logged. The engine's own progress rides the SchemaSmith_StatusMessages sidecar.
SELECT 'Course 8 M6: MySQL user scripts have no progress channel.' AS note;

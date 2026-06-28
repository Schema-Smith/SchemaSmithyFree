-- Precondition: represents state that already lives on the target server, independent of your
-- schema package. Run this once before quenching to simulate a server whose feature flags were
-- set by an app or an operator. The query token in the package reads these rows at deploy time.
CREATE TABLE IF NOT EXISTS FeatureFlag (FlagName VARCHAR(50) NOT NULL PRIMARY KEY, Enabled TINYINT NOT NULL);

INSERT IGNORE INTO FeatureFlag (FlagName, Enabled)
VALUES ('Billing', 1), ('Reporting', 1), ('BetaSearch', 0);

-- Precondition: represents state that already lives on the target server, independent of your
-- schema package. Run this once before quenching to simulate a server whose feature flags were
-- set by an app or an operator. The query token in the package reads these rows at deploy time.
CREATE TABLE IF NOT EXISTS public.featureflag (flagname VARCHAR(50) NOT NULL PRIMARY KEY, enabled BOOLEAN NOT NULL);

INSERT INTO public.featureflag (flagname, enabled)
VALUES ('Billing', true), ('Reporting', true), ('BetaSearch', false)
ON CONFLICT (flagname) DO NOTHING;

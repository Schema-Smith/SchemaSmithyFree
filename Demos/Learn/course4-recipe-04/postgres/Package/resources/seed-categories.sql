-- Reference data kept in its own file and embedded into a deploy script via the File token.
-- Idempotent: only inserts categories that aren't already present.
INSERT INTO public.category (categoryname)
VALUES ('Books'), ('Electronics'), ('Garden')
ON CONFLICT (categoryname) DO NOTHING;

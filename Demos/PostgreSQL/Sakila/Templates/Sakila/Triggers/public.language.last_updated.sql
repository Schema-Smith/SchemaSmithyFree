DROP TRIGGER IF EXISTS last_updated ON public.language;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.language FOR EACH ROW EXECUTE FUNCTION last_updated()
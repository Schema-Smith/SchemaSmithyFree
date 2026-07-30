DROP TRIGGER IF EXISTS last_updated ON public.country;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.country FOR EACH ROW EXECUTE FUNCTION last_updated()
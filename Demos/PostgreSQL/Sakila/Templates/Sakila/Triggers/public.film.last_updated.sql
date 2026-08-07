DROP TRIGGER IF EXISTS last_updated ON public.film;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.film FOR EACH ROW EXECUTE FUNCTION last_updated()
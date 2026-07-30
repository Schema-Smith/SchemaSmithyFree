DROP TRIGGER IF EXISTS last_updated ON public.rental;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.rental FOR EACH ROW EXECUTE FUNCTION last_updated()
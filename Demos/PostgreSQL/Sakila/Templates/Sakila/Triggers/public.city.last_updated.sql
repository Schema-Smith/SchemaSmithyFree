DROP TRIGGER IF EXISTS last_updated ON public.city;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.city FOR EACH ROW EXECUTE FUNCTION last_updated()
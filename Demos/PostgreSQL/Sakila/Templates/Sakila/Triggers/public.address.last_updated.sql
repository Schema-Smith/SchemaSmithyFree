DROP TRIGGER IF EXISTS last_updated ON public.address;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.address FOR EACH ROW EXECUTE FUNCTION last_updated()
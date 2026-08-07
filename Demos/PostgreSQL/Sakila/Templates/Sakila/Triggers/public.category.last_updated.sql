DROP TRIGGER IF EXISTS last_updated ON public.category;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.category FOR EACH ROW EXECUTE FUNCTION last_updated()
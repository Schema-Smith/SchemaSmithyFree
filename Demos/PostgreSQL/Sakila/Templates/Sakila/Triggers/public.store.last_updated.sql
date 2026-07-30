DROP TRIGGER IF EXISTS last_updated ON public.store;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.store FOR EACH ROW EXECUTE FUNCTION last_updated()
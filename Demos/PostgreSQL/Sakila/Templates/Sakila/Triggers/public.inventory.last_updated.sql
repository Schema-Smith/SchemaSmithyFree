DROP TRIGGER IF EXISTS last_updated ON public.inventory;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.inventory FOR EACH ROW EXECUTE FUNCTION last_updated()
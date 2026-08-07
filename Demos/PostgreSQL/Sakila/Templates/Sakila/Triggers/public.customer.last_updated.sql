DROP TRIGGER IF EXISTS last_updated ON public.customer;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.customer FOR EACH ROW EXECUTE FUNCTION last_updated()
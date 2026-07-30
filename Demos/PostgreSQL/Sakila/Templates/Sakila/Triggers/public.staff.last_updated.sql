DROP TRIGGER IF EXISTS last_updated ON public.staff;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.staff FOR EACH ROW EXECUTE FUNCTION last_updated()
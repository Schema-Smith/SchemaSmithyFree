DROP TRIGGER IF EXISTS last_updated ON public.actor;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.actor FOR EACH ROW EXECUTE FUNCTION last_updated()
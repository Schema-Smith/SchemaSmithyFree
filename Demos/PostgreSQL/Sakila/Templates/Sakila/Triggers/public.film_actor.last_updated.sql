DROP TRIGGER IF EXISTS last_updated ON public.film_actor;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.film_actor FOR EACH ROW EXECUTE FUNCTION last_updated()
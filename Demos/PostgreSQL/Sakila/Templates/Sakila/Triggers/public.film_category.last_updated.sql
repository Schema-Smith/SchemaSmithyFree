DROP TRIGGER IF EXISTS last_updated ON public.film_category;
CREATE TRIGGER last_updated BEFORE UPDATE ON public.film_category FOR EACH ROW EXECUTE FUNCTION last_updated()
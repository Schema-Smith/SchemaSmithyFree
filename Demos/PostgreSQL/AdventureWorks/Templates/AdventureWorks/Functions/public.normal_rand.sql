CREATE OR REPLACE FUNCTION public.normal_rand(integer, double precision, double precision)
 RETURNS SETOF double precision
 LANGUAGE c
 STRICT
AS '$libdir/tablefunc', $function$normal_rand$function$

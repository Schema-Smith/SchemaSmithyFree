CREATE OR REPLACE FUNCTION public.crosstab4(text)
 RETURNS SETOF tablefunc_crosstab_4
 LANGUAGE c
 STABLE STRICT
AS '$libdir/tablefunc', $function$crosstab$function$

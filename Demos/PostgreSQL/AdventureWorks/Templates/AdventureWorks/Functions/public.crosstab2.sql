CREATE OR REPLACE FUNCTION public.crosstab2(text)
 RETURNS SETOF tablefunc_crosstab_2
 LANGUAGE c
 STABLE STRICT
AS '$libdir/tablefunc', $function$crosstab$function$

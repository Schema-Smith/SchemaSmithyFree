CREATE OR REPLACE FUNCTION public.crosstab3(text)
 RETURNS SETOF tablefunc_crosstab_3
 LANGUAGE c
 STABLE STRICT
AS '$libdir/tablefunc', $function$crosstab$function$

CREATE OR REPLACE FUNCTION public.crosstab(text, text)
 RETURNS SETOF record
 LANGUAGE c
 STABLE STRICT
AS '$libdir/tablefunc', $function$crosstab_hash$function$

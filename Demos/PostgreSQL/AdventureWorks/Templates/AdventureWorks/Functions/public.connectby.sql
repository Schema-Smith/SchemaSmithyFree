CREATE OR REPLACE FUNCTION public.connectby(text, text, text, text, text, integer)
 RETURNS SETOF record
 LANGUAGE c
 STABLE STRICT
AS '$libdir/tablefunc', $function$connectby_text_serial$function$

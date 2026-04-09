-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."StripParenWrapping"(p_text TEXT) RETURNS TEXT
    LANGUAGE plpgsql
AS $$
DECLARE
  s text := TRIM(p_text);
  len int;
  i int;
  cnt int;
  ch text;
  match_pos int;
BEGIN
  IF s IS NULL THEN RETURN NULL; END IF;
  LOOP len := LENGTH(s); EXIT WHEN len < 2;
    IF SUBSTRING(s, 1, 1) <> '(' OR substring(s, len, 1) <> ')' THEN EXIT; END IF;
    cnt := 1;
    match_pos := 0;
    FOR i IN 2..len LOOP
      ch := SUBSTRING(s, i, 1);
      IF ch = '(' THEN
        cnt := cnt + 1;
      ELSIF ch = ')' THEN
        cnt := cnt - 1;
      END IF;
      IF cnt = 0 THEN
        match_pos := i;
        EXIT;
      END IF;
    END LOOP;
    -- only strip if the first '(' matches the final ')' of the string
    IF match_pos = len THEN s := TRIM(SUBSTRING(s, 2, len - 2)); ELSE EXIT; END IF;
  END LOOP;
  
RETURN s;
END;
$$ IMMUTABLE;
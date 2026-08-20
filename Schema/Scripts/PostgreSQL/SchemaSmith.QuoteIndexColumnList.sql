-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."QuoteIndexColumnList"(p_List TEXT)
    RETURNS TEXT
    LANGUAGE plpgsql
AS $$
DECLARE
    parts TEXT[] := ARRAY[]::TEXT[];
    item TEXT;
    s TEXT;
    token TEXT;
    mods TEXT;
    col TEXT;
    col_and_opclass TEXT[];
    res_items TEXT[] := ARRAY[]::TEXT[];

    -- tokenizer state
    len INT;
    i INT;
    ch TEXT;
    cur TEXT := '';
    paren_level INT := 0;
    in_double BOOLEAN := FALSE;
    in_single BOOLEAN := FALSE;
BEGIN
    IF p_List IS NULL OR TRIM(p_List) = '' THEN
        RETURN NULL;
    END IF;

    -- Split on top-level commas only (ignore commas inside parentheses or inside quoted literals)
    len := CHAR_LENGTH(p_List);
    i := 1;
    cur := '';
    paren_level := 0;
    in_double := FALSE;
    in_single := FALSE;

    WHILE i <= len LOOP
        ch := SUBSTR(p_List, i, 1);

        IF ch = '"' AND NOT in_single THEN
            -- Handle doubled double-quotes inside quoted identifier/string
            IF in_double AND i < len AND SUBSTR(p_List, i + 1, 1) = '"' THEN
                cur := cur || '""';
                i := i + 2;
                CONTINUE;
            ELSE
                in_double := NOT in_double;
                cur := cur || ch;
                i := i + 1;
                CONTINUE;
            END IF;
        ELSIF ch = '''' AND NOT in_double THEN
            -- Handle doubled single-quotes inside string literal
            IF in_single AND i < len AND SUBSTR(p_List, i + 1, 1) = '''' THEN
                cur := cur || '''''';
                i := i + 2;
                CONTINUE;
            ELSE
                in_single := NOT in_single;
                cur := cur || ch;
                i := i + 1;
                CONTINUE;
            END IF;
        ELSIF ch = '(' AND NOT in_single AND NOT in_double THEN
            paren_level := paren_level + 1;
            cur := cur || ch;
            i := i + 1;
            CONTINUE;
        ELSIF ch = ')' AND NOT in_single AND NOT in_double THEN
            IF paren_level > 0 THEN
                paren_level := paren_level - 1;
            END IF;
            cur := cur || ch;
            i := i + 1;
            CONTINUE;
        ELSIF ch = ',' AND paren_level = 0 AND NOT in_double AND NOT in_single THEN
            parts := ARRAY_APPEND(parts, cur);
            cur := '';
            i := i + 1;
            CONTINUE;
        ELSE
            cur := cur || ch;
            i := i + 1;
        END IF;
    END LOOP;

    -- append last token
    parts := ARRAY_APPEND(parts, cur);

    -- Process each item: strip trailing modifiers, normalize spaces inside modifiers,
    -- trim surrounding spaces/double-quotes from identifier, escape internal quotes, re-quote, reattach modifiers
    FOREACH item IN ARRAY parts LOOP
        s := TRIM(item);
        mods := '';

        LOOP
            EXIT WHEN s = '' OR NOT (
                s ~* '\s+(WITHOUT\s+OVERLAPS)\s*$' OR
                s ~* '\s+(NULLS\s+FIRST)\s*$' OR
                s ~* '\s+(NULLS\s+LAST)\s*$' OR
                s ~* '\s+(ASC|DESC)\s*$'
            );

            token := SUBSTRING(s FROM '(?i)(WITHOUT\s+OVERLAPS|NULLS\s+FIRST|NULLS\s+LAST|ASC|DESC)\s*$');
            s := REGEXP_REPLACE(s, '(?i)\s*(WITHOUT\s+OVERLAPS|NULLS\s+FIRST|NULLS\s+LAST|ASC|DESC)\s*$', '');
            token := TRIM(token);
            -- collapse duplicate internal whitespace inside the modifier
            token := REGEXP_REPLACE(token, '\s+', ' ', 'g');

            -- Prepend token to preserve original left-to-right order (we remove from the end)
            IF mods = '' THEN
                mods := token;
            ELSE
                mods := token || ' ' || mods;
            END IF;

            s := RTRIM(s);
        END LOOP;

        IF s NOT LIKE '%(%' THEN
          -- A plain column may carry a trailing operator class (CREATE INDEX ... USING gist
          -- (embedding vector_l2_ops)); split it off before quoting, or it folds into the quoted
          -- identifier as one bogus two-word name instead of "embedding" vector_l2_ops.
          col_and_opclass := REGEXP_MATCH(s, '^("(?:[^"]|"")+"|[A-Za-z_][A-Za-z0-9_$]*)\s+([A-Za-z_][A-Za-z0-9_$]*(?:\.[A-Za-z_][A-Za-z0-9_$]*)?)$');
          IF col_and_opclass IS NOT NULL THEN
            col := '"' || TRIM(BOTH ' "' FROM col_and_opclass[1]) || '" ' || col_and_opclass[2];
          ELSE
            -- Clean column identifier: remove surrounding spaces and double quotes, then re-quote
            col := '"' || TRIM(BOTH ' "' FROM s) || '"';
          END IF;
        ELSE
          col := TRIM(s);  -- Preserve complex expressions as-is
        END IF;
        res_items := ARRAY_APPEND(res_items, col || (CASE WHEN mods <> '' THEN ' ' || UPPER(mods) ELSE '' END));
    END LOOP;

    RETURN ARRAY_TO_STRING(res_items, ',');
END $$;

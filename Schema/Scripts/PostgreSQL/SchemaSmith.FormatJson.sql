-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."FormatJson"(p_json JSON, p_indent_size INTEGER DEFAULT 2, p_step INTEGER DEFAULT 0)
    RETURNS JSON
AS $$
DECLARE
    v_type TEXT;
    v_text TEXT := '';
    v_indent INTEGER;
    v_key TEXT;
    v_object JSON;
    v_count INTEGER;
BEGIN
    v_indent := COALESCE(p_indent_size, 4);
    p_step := COALESCE(p_step, 0);
    -- Object or array?
    v_type := JSON_TYPEOF(p_json);

    IF v_type = 'object' THEN
        v_text := E'{\n';
        -- Count only the keys that will actually be EMITTED. Counting every key and decrementing before
        -- the null-skip below emits a separator after the last surviving key whenever the keys after it
        -- are all null, producing `{ "a": 1, }` -- not JSON, and the cast at the end of this function
        -- rejects it. Unreachable until a nullable property was added last; correct regardless.
        SELECT COUNT(*) INTO v_count
          FROM (SELECT JSON_OBJECT_KEYS(p_json) AS k) keys
         WHERE (p_json->keys.k)::TEXT != 'null';
        FOR v_key IN (SELECT JSON_OBJECT_KEYS(p_json)) LOOP
                CONTINUE WHEN ((p_json->v_key)::TEXT = 'null');
                v_count := v_count - 1;
                v_text := v_text || REPEAT(' ', v_indent * (p_step + 1))  || TO_JSON(v_key)::TEXT || ': ' || "SchemaSmith"."FormatJson"(p_json->v_key, p_indent_size, p_step + 1);
                IF v_count > 0 THEN
                    v_text := v_text || ',';
                END IF;
                v_text := v_text || E'\n';
            END LOOP;
        v_text := v_text || REPEAT(' ', (v_indent * p_step)) || '}';
    ELSIF v_type = 'array' THEN
        v_text := E'[\n';
        v_count := JSON_ARRAY_LENGTH(p_json) - 1;
        FOR v_object IN (SELECT JSON_ARRAY_ELEMENTS(p_json)) LOOP
                v_text := v_text || REPEAT(' ', v_indent * (p_step + 1))  || "SchemaSmith"."FormatJson"(v_object, p_indent_size, p_step + 1);
                IF v_count > 0 THEN
                    v_text := v_text || ',';
                    v_count := v_count - 1;
                END IF;
                v_text := v_text || E'\n';
            END LOOP;
        v_text := v_text || REPEAT(' ', (v_indent * p_step)) || ']';
    ELSE -- A simple value
        v_text := v_text || p_json::TEXT;
    END IF;

    IF p_step > 0 THEN
        RETURN v_text;
    ELSE
        RETURN v_text::JSON;
    END IF;
END;
$$ LANGUAGE plpgsql;
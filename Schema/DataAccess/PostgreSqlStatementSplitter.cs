// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Text;

namespace Schema.DataAccess;

/// <summary>
/// Splits PostgreSQL scripts into individual statements, respecting dollar-quoted blocks.
/// Both $$ and named tags like $function$ or $body$ are recognized as dollar-quote delimiters.
/// Semicolons inside dollar-quoted blocks are not treated as statement boundaries.
/// </summary>
public static class PostgreSqlStatementSplitter
{
    public static List<string> Split(string script)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(script)) return results;

        var current = new StringBuilder();
        string dollarTag = null;
        var i = 0;

        while (i < script.Length)
        {
            // Check for a dollar-quote tag starting at position i
            if (script[i] == '$')
            {
                var tag = ReadDollarTag(script, i);
                if (tag != null)
                {
                    if (dollarTag == null)
                    {
                        // Opening a dollar-quote block
                        dollarTag = tag;
                        current.Append(tag);
                        i += tag.Length;
                        continue;
                    }
                    if (tag == dollarTag)
                    {
                        // Closing the dollar-quote block
                        dollarTag = null;
                        current.Append(tag);
                        i += tag.Length;
                        continue;
                    }
                }
            }

            // Semicolon outside dollar quotes = statement boundary
            if (script[i] == ';' && dollarTag == null)
            {
                current.Append(';');
                var statement = current.ToString().Trim();
                if (!string.IsNullOrEmpty(statement))
                    results.Add(statement);
                current.Clear();
                i++;
                continue;
            }

            current.Append(script[i]);
            i++;
        }

        // Remaining content (no trailing semicolon)
        var remainder = current.ToString().Trim();
        if (!string.IsNullOrEmpty(remainder))
            results.Add(remainder);

        return results;
    }

    /// <summary>
    /// Reads a dollar-quote tag starting at position start in script.
    /// Returns the full tag (e.g., "$$", "$function$", "$body$") or null if not a valid tag.
    /// A valid tag starts with $, contains only letters, digits, or underscores (or nothing), and ends with $.
    /// </summary>
    private static string ReadDollarTag(string script, int start)
    {
        if (script[start] != '$') return null;

        var j = start + 1;
        while (j < script.Length && script[j] != '$')
        {
            var c = script[j];
            // Tag body may only contain letters, digits, or underscores
            if (!char.IsLetterOrDigit(c) && c != '_')
                return null;
            j++;
        }

        if (j >= script.Length) return null; // No closing $
        // j is now at the closing $
        return script.Substring(start, j - start + 1);
    }
}

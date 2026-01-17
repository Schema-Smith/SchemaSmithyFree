using System;
using System.Collections.Generic;
using System.Text;

namespace Schema.DataAccess;

public static class SqlHelpers
{
    public static List<string> SplitIntoBatches(string script)
    {
        var batches = new List<string>();
        var lines = (script + "\nGO") // make sure we end with a GO
            .Replace("\r\n", "\n").Replace("\r", "\n") // normalize line endings
            .Split(['\n'], StringSplitOptions.None);
        var batch = new StringBuilder();
        var inMultiLine = false;
        var inIdentifier = false;
        var inString = false;
        var inString2 = false;
        foreach (var line in lines)
        {
            var cleanLine = CleanseLine(line, ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            if (cleanLine == "GO")
            {
                var batchStr = batch.ToString();
                if (string.IsNullOrWhiteSpace(batchStr)) continue; // Skip empty batches
                batches.Add(batchStr);
                batch.Clear();
            }
            else
            {
                batch.AppendLine(line);
            }
        }

        if (!string.IsNullOrWhiteSpace(batch.ToString()) && batch.Length > 0)
            throw new Exception("Batch Parsing Failed: " + (inString ? "Unterminated single quote string" : inString2 ? "Unterminated double quote string" : inMultiLine ? "Unterminated comment" : inIdentifier ? "Unterminated identifier": ""));
        return batches;
    }

    // Handle strings in comments and comments in strings while hunting for batch terminators
    private static string CleanseLine(string line, ref bool inString, ref bool inString2, ref bool inMultiLine, ref bool inIdentifier)
    {
        var cleanLine = line;

        if (inString)
        {
            if (cleanLine.Contains("'"))
            {
                inString = false;
                return CleanseLine(cleanLine.Substring(cleanLine.IndexOf("'", StringComparison.Ordinal) + 1), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }

            cleanLine = "";
        }
        else if (inString2)
        {
            if (cleanLine.Contains("\""))
            {
                inString2 = false;
                return CleanseLine(cleanLine.Substring(cleanLine.IndexOf("\"", StringComparison.Ordinal) + 1), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }

            cleanLine = "";
        }
        else if (inMultiLine)
        {
            if (cleanLine.Contains("*/"))
            {
                inMultiLine = false;
                return CleanseLine(cleanLine.Substring(cleanLine.IndexOf("*/", StringComparison.Ordinal) + 2), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }

            cleanLine = "";
        }
        else if (inIdentifier)
        {
            if (cleanLine.Contains("]"))
            {
                inIdentifier = false;
                return CleanseLine(cleanLine.Substring(cleanLine.IndexOf("]", StringComparison.Ordinal) + 1), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }
            cleanLine = "";
        }
        else
        {
            var s = cleanLine.IndexOf("'", StringComparison.Ordinal);
            var s2 = cleanLine.IndexOf("\"", StringComparison.Ordinal);
            var sc = cleanLine.IndexOf("--", StringComparison.Ordinal);
            var mc = cleanLine.IndexOf("/*", StringComparison.Ordinal);
            var id = cleanLine.IndexOf("[", StringComparison.Ordinal);
            if (s > -1 && (s2 > s || s2 == -1) && (mc > s || mc == -1) && (sc > s || sc == -1) && (id > s || id == -1))
            {
                inString = true;
                return CleanseLine(cleanLine.Substring(s + 1), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }

            if (s2 > -1 && (mc > s2 || mc == -1) && (sc > s2 || sc == -1) && (id > s2 || id == -1))
            {
                inString2 = true;
                return CleanseLine(cleanLine.Substring(s2 + 1), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }

            if (mc > -1 && (sc > mc || sc == -1) && (id > mc || id == -1))
            {
                inMultiLine = true;
                return CleanseLine(cleanLine.Substring(mc + 2), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }

            if (id > -1 && (sc > id || sc == -1))
            {
                inIdentifier = true;
                return CleanseLine(cleanLine.Substring(id + 1), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
            }

            if (sc > -1)
                return CleanseLine(cleanLine.Substring(0, sc), ref inString, ref inString2, ref inMultiLine, ref inIdentifier);
        }

        return cleanLine.Trim('\t', ' ').ToUpper();
    }
}
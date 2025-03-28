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
            .Split(new[] { '\n' }, StringSplitOptions.None);
        var batch = new StringBuilder();
        var inMultiLine = false;
        var inString = false;
        foreach (var line in lines)
        {
            var cleanLine = CleanseLine(line, ref inString, ref inMultiLine);
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
            throw new Exception("Batch Parsing Failed");
        return batches;
    }

    // Handle strings in comments and comments in strings while hunting for batch terminators
    private static string CleanseLine(string line, ref bool inString, ref bool inMultiLine)
    {
        var cleanLine = line;

        if (inString)
        {
            if (cleanLine.Contains("'"))
            {
                inString = false;
                return CleanseLine(cleanLine.Substring(cleanLine.IndexOf("'") + 1), ref inString, ref inMultiLine);
            }

            cleanLine = "";
        }
        else if (inMultiLine)
        {
            if (cleanLine.Contains("*/"))
            {
                inMultiLine = false;
                return CleanseLine(cleanLine.Substring(cleanLine.IndexOf("*/") + 2), ref inString, ref inMultiLine);
            }

            cleanLine = "";
        }
        else
        {
            var s = cleanLine.IndexOf("'");
            var sc = cleanLine.IndexOf("--");
            var mc = cleanLine.IndexOf("/*");
            if (s > -1 && (mc > s || mc == -1) && (sc > s || sc == -1))
            {
                inString = true;
                return CleanseLine(cleanLine.Substring(s + 1), ref inString, ref inMultiLine);
            }

            if (mc > -1 && (mc > sc || sc == -1))
            {
                inMultiLine = true;
                return CleanseLine(cleanLine.Substring(mc + 2), ref inString, ref inMultiLine);
            }

            if (sc > -1)
                return CleanseLine(cleanLine.Substring(0, sc), ref inString, ref inMultiLine);
        }

        return cleanLine.Trim('\t', ' ').ToUpper();
    }
}
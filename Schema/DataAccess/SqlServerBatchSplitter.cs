// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Text;

namespace Schema.DataAccess;

public static class SqlServerBatchSplitter
{
    /// <summary>
    /// Split a T-SQL script into batches on the GO separator. GO is a client/tooling directive (SSMS,
    /// sqlcmd) that ADO.NET does not recognize, so the kindle path must split on it before executing. This
    /// lets a kindled object use the pre-2016 idempotent create form — IF OBJECT_ID(...) DROP; GO; CREATE … —
    /// instead of CREATE OR ALTER (a 2016 SP1 feature) that would fail to parse on the SQL Server 2008 floor.
    /// A separator is a line that is exactly GO (optionally "GO &lt;count&gt;"), case-insensitive; GO anywhere
    /// else (inside a string or identifier) is left untouched. A script with no GO yields a single batch.
    /// </summary>
    public static List<string> Split(string script)
    {
        var batches = new List<string>();
        var current = new StringBuilder();
        foreach (var line in script.Split('\n'))
        {
            if (IsBatchSeparator(line))
            {
                if (current.ToString().Trim().Length > 0) batches.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(line).Append('\n');
            }
        }
        if (current.ToString().Trim().Length > 0) batches.Add(current.ToString());
        return batches;
    }

    private static bool IsBatchSeparator(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase)) return true;
        // "GO <count>" — SSMS/sqlcmd repeat form; treat as a plain separator (the count is a tooling nicety).
        return trimmed.StartsWith("GO ", StringComparison.OrdinalIgnoreCase)
               && int.TryParse(trimmed.Substring(3).Trim(), out _);
    }
}

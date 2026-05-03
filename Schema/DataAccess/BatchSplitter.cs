// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;

namespace Schema.DataAccess;

public static class BatchSplitter
{
    /// <summary>
    /// Split a script that uses DELIMITER commands.
    /// MySQL client handles DELIMITER but ADO.NET does not, so we need to parse it.
    /// </summary>
    public static List<string> Split(string script)
    {
        var batches = new List<string>();
        if (script.Contains("DELIMITER"))
            SplitDelimitedScript(batches, script);
        else
            batches.Add(script);

        return batches;
    }

    private static void SplitDelimitedScript(List<string> batches, string script)
    {
        // Split by DELIMITER commands and execute each part
        var currentDelimiter = ";";
        var lines = script.Split('\n');
        var currentStatement = "";

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Check for DELIMITER command
            if (trimmedLine.StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
            {
                // Execute any pending statement before changing delimiter
                if (!string.IsNullOrWhiteSpace(currentStatement))
                {
                    batches.Add(currentStatement.Trim());
                    currentStatement = "";
                }

                // Change delimiter
                currentDelimiter = trimmedLine.Substring(10).Trim();
                continue;
            }

            // Add line to current statement
            currentStatement += line + "\n";

            // Check if statement is complete
            if (currentDelimiter != ";" && trimmedLine.EndsWith(currentDelimiter))
            {
                // Remove the custom delimiter from the end
                currentStatement = currentStatement.Trim();
                if (currentStatement.EndsWith(currentDelimiter)) currentStatement = currentStatement.Substring(0, currentStatement.Length - currentDelimiter.Length);

                if (!string.IsNullOrWhiteSpace(currentStatement)) batches.Add(currentStatement.Trim());
                currentStatement = "";
            }
            else if (currentDelimiter == ";" && trimmedLine.EndsWith(";"))
            {
                // Standard semicolon delimiter
                if (!string.IsNullOrWhiteSpace(currentStatement)) batches.Add(currentStatement.Trim());
                currentStatement = "";
            }
        }

        if (!string.IsNullOrWhiteSpace(currentStatement)) batches.Add(currentStatement.Trim());
    }
}

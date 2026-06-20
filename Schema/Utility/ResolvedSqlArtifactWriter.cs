// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Schema.Isolators;

namespace Schema.Utility;

/// <summary>
/// Writes the fully token-expanded SQL of a failed script to a re-runnable artifact file. Raw by
/// default (the local operator already holds the secrets and needs the real SQL to reproduce); an
/// opt-in scrubbed variant redacts sensitive token values + inline connection-string credentials for
/// safe ticket/CI attachment. The shippable log carries only the returned path, never the SQL itself.
/// </summary>
public static class ResolvedSqlArtifactWriter
{
    private const int MinRedactLength = 4; // avoid over-redacting trivial values that collide with common SQL text

    public static string BuildArtifact(string header, IReadOnlyList<string> batches, int failingBatchIndex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- ============================================================");
        sb.AppendLine($"-- {header}");
        sb.AppendLine("-- Contains expanded values — may be sensitive. Local debugging; scrub before sharing.");
        sb.AppendLine("-- ============================================================");
        for (var i = 0; i < batches.Count; i++)
        {
            if (i == failingBatchIndex) sb.AppendLine($"-- >>> FAILING BATCH (#{i + 1}) >>>");
            sb.AppendLine(batches[i]);
            sb.AppendLine("GO");
        }
        return sb.ToString();
    }

    public static string Scrub(string sql, IReadOnlyList<KeyValuePair<string, string>> sensitiveTokenValues)
    {
        var scrubbed = sql;
        foreach (var kv in sensitiveTokenValues.Where(kv => kv.Value is { Length: >= MinRedactLength }))
            scrubbed = scrubbed.Replace(kv.Value, LogScrubber.Mask);
        return LogScrubber.ScrubConnectionStringSubfields(scrubbed);
    }

    /// <returns>The full path written, for surfacing in the log.</returns>
    public static string Write(string directory, string fileName, string content)
    {
        var path = Path.Combine(directory, fileName);
        ProductFileWrapper.GetFromFactory().WriteAllText(path, content);
        return path;
    }
}

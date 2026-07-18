// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Delivery;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.Validation.Checks;

/// <summary>
/// Leans authors toward the canonical table-file naming convention
/// <c>&lt;schema&gt;.&lt;table&gt;[.&lt;VariantName&gt;].json</c> (schema segment omitted for
/// schema-scrubbed / MySQL tables). Identity lives in the file content, so a non-canonical name
/// never breaks a deploy — it only earns a warning (<c>SS-FILE-NAME-003</c>) so the filename stays
/// an honest, sortable pointer to the table it holds. Walks every <c>*.json</c> under a
/// <c>Tables/</c> folder below the package root.
/// </summary>
public sealed class TableFileNameCheck : ISchemaCheck
{
    private const string Code = "SS-FILE-NAME-003";
    private const string Category = "FileName";

    public IEnumerable<Finding> Run(ValidationContext ctx)
    {
        var findings = new List<Finding>();
        var dir = ProductDirectoryWrapper.GetFromFactory();
        if (!dir.Exists(ctx.PackagePath)) return findings;

        foreach (var path in dir.GetFiles(ctx.PackagePath, "*.json", SearchOption.AllDirectories))
        {
            if (!IsUnderTablesFolder(path)) continue;

            Table table;
            try { table = JsonHelper.TableLoad(path, ctx.Platform); }
            catch { continue; } // malformed content is another check's concern
            if (table == null || string.IsNullOrWhiteSpace(table.Name)) continue;

            var schema = (table as IDeliverableTable)?.Schema ?? "";
            var canonical = TableFileName.Canonical(schema, table.Name, table.VariantName ?? "", isSchemaTemplate: string.IsNullOrEmpty(schema));
            var actual = Path.GetFileName(path);
            if (!string.Equals(actual, canonical, StringComparison.OrdinalIgnoreCase))
                findings.Add(new Finding(Severity.Warning, Code, Category, path,
                    $"Table file '{actual}' does not match its canonical name '{canonical}' (from Schema/Name/VariantName). Identity is content, so this is a naming lean, not an error — rename to keep the file a reliable pointer to its table."));
        }

        return findings;
    }

    private static bool IsUnderTablesFolder(string path) =>
        path.Split('\\', '/').Any(segment => segment.Equals("Tables", StringComparison.Ordinal));
}

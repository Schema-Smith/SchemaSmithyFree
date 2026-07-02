// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Schema.Domain;
using Schema.Domain.SqlServer;
using SchemaQuench.Validation;

namespace SchemaQuench.UnitTests.Validation;

/// <summary>
/// Shared in-memory package fixtures for check tests (Slice 1B + later Slice 2 checks).
/// Builds a <see cref="LoadedPackage"/> directly — no disk I/O, no <see cref="PackageLoader"/> —
/// since templates are passed straight into <see cref="ValidationContext"/> rather than attached
/// to <see cref="Product"/>.
/// </summary>
public static class ValidationTestPackages
{
    /// <summary>
    /// One template with one table (one column) on the given platform. Only
    /// <see cref="Platform.SqlServer"/> is implemented today — add platform branches here as
    /// later slices need them rather than guessing ahead of demand.
    /// </summary>
    public static LoadedPackage Minimal(Platform platform)
    {
        var product = new Product { Name = "TestProduct", Platform = platform, TemplateOrder = new List<string>() };
        var template = new Template { Name = "TestTemplate" };

        Table table = platform switch
        {
            Platform.SqlServer => new SqlServerTable
            {
                Name = "Customer",
                Columns = { new SqlServerColumn { Name = "Id", DataType = "int" } }
            },
            _ => throw new NotSupportedException($"ValidationTestPackages.Minimal does not yet support platform '{platform}'.")
        };
        template.Tables.Add(table);

        return new LoadedPackage(product, new[] { template });
    }
}

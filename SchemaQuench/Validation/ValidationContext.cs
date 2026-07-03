// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Schema.Domain;

namespace SchemaQuench.Validation;

/// <summary>
/// The read-only view of a loaded package handed to every <see cref="ISchemaCheck"/>. Flattens
/// all templates' tables into <see cref="AllTables"/> once at construction so checks don't each
/// re-walk the template list.
/// </summary>
public sealed class ValidationContext
{
    public Product Product { get; }
    public IReadOnlyList<Template> Templates { get; }
    public Platform Platform => Product.Platform;
    public string PackagePath { get; }
    public IReadOnlyList<Table> AllTables { get; }

    public ValidationContext(Product product, IReadOnlyList<Template> templates, string packagePath)
    {
        Product = product;
        Templates = templates;
        PackagePath = packagePath;
        AllTables = templates.SelectMany(t => t.Tables).ToList();
    }
}

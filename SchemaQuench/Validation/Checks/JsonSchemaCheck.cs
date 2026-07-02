// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.Validation.Checks;

/// <summary>
/// The final `--Validate` check: two passes over the committed <c>.json-schemas/{type}.{platform}.
/// schema</c> files.
/// <para>
/// Pass 1 (staleness, runs first): regenerates each committed schema in memory via
/// <see cref="SchemaGenerator.GenerateSchema(Type, Func{Type, Type})"/>, re-merges the committed
/// file's hand-authored <c>Extensions</c> fragments back in via
/// <see cref="SchemaGenerator.MergeExtensionsDefinition"/>, and compares the result to the
/// committed file with <see cref="JToken.DeepEquals(JToken, JToken)"/>. A mismatch means the
/// domain model moved on since <c>--WriteSchemasOnly</c> was last run — <c>SS-STALE-001</c> — and
/// Pass 2 is skipped for that type: structural results against a schema that no longer matches
/// the model would be misleading.
/// </para>
/// <para>
/// Pass 2 (structural + custom-property governance, non-stale types only): every package JSON
/// file is validated against its committed schema via NJsonSchema. The generator's
/// <c>additionalProperties: false</c> catches misnamed/misplaced properties, <c>required</c>
/// catches missing ones, and any hand-authored governance on custom <c>Extensions</c> properties
/// (an authored <c>enum</c>/<c>required</c> fragment) is enforced automatically because Pass 2
/// validates against the COMMITTED file, not a freshly generated one.
/// </para>
/// The file/type/platform mapping uses the public
/// <see cref="Schema.Utility.RepositoryHelper.GetSchemaFileNames(Platform)"/> directly. The
/// domain-type resolution mirrors <c>RepositoryHelper</c>'s private <c>GetTypeForSchemaFile</c> /
/// <c>PlatformElementResolver</c> — duplicated here rather than exposed from <c>Schema/</c>,
/// which this check must not modify.
/// </summary>
public sealed class JsonSchemaCheck : ISchemaCheck
{
    private const string StaleCode = "SS-STALE-001";
    private const string StaleCategory = "Staleness";
    private const string JsonCode = "SS-JSON-001";
    private const string JsonCategory = "JsonSchema";

    public IEnumerable<Finding> Run(ValidationContext ctx)
    {
        var dir = ProductDirectoryWrapper.GetFromFactory();
        var file = ProductFileWrapper.GetFromFactory();
        var schemaDir = Path.Combine(ctx.PackagePath, ".json-schemas");

        var findings = new List<Finding>();
        if (!dir.Exists(schemaDir)) return findings;

        var staleTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schemaByType = new Dictionary<string, JsonSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileName in RepositoryHelper.GetSchemaFileNames(ctx.Platform))
        {
            var schemaPath = Path.Combine(schemaDir, fileName);
            if (!file.Exists(schemaPath)) continue; // nothing committed for this type — nothing to check

            JObject committed;
            try
            {
                committed = JObject.Parse(file.ReadAllText(schemaPath));
            }
            catch
            {
                continue; // malformed committed schema file isn't this check's concern
            }

            var typeName = fileName.Split('.')[0];
            var domainType = GetTypeForSchemaFile(fileName, ctx.Platform);
            var freshGenerated = SchemaGenerator.GenerateSchema(domainType, PlatformElementResolver(ctx.Platform));
            var merged = SchemaGenerator.MergeExtensionsDefinition(freshGenerated, committed);

            if (!JToken.DeepEquals(merged, committed))
            {
                findings.Add(new Finding(Severity.Error, StaleCode, StaleCategory, schemaPath,
                    $"{schemaPath}: committed .json-schemas are stale — regenerate via --WriteSchemasOnly."));
                staleTypes.Add(typeName); // short-circuit: Pass 2 skips this type entirely
                continue;
            }

            schemaByType[typeName] = LoadNJsonSchema(committed);
        }

        if (schemaByType.Count == 0) return findings;

        var jsonFiles = dir.GetFiles(ctx.PackagePath, "*.json", SearchOption.AllDirectories)
            .Where(f => !IsUnderJsonSchemasDir(f))
            .ToList();

        foreach (var jsonFile in jsonFiles)
        {
            var typeName = MapFileToType(jsonFile);
            if (typeName == null || staleTypes.Contains(typeName)) continue;
            if (!schemaByType.TryGetValue(typeName, out var schema)) continue;

            var text = SafeReadText(file, jsonFile);
            if (text == null) continue;

            foreach (var error in FlattenErrors(SafeValidate(schema, text)))
                findings.Add(new Finding(Severity.Error, JsonCode, JsonCategory, jsonFile,
                    $"{jsonFile}: {RenderMessage(error)}"));
        }

        return findings;
    }

    private static JsonSchema LoadNJsonSchema(JObject committed) =>
        JsonSchema.FromJsonAsync(committed.ToString()).GetAwaiter().GetResult();

    private static ICollection<ValidationError> SafeValidate(JsonSchema schema, string jsonText)
    {
        try
        {
            return schema.Validate(jsonText);
        }
        catch
        {
            // Malformed package JSON is TokenCheck/package-load's concern, not this one — skip
            // rather than crash the whole --Validate run over one bad file.
            return Array.Empty<ValidationError>();
        }
    }

    private static string SafeReadText(IFile file, string path)
    {
        try
        {
            return file.Exists(path) ? file.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    // NJsonSchema wraps every nested object/array violation in a ChildSchemaValidationError whose
    // Errors dictionary holds the real per-property leaf errors — Kind/Property/Path only carry
    // useful information at the leaf, so descend until one is reached.
    private static IEnumerable<ValidationError> FlattenErrors(IEnumerable<ValidationError> errors)
    {
        foreach (var error in errors)
        {
            if (error is ChildSchemaValidationError child)
            {
                foreach (var leaf in FlattenErrors(child.Errors.Values.SelectMany(v => v)))
                    yield return leaf;
            }
            else
            {
                yield return error;
            }
        }
    }

    private static string RenderMessage(ValidationError error) => error.Kind switch
    {
        ValidationErrorKind.NoAdditionalPropertiesAllowed => $"unexpected property '{error.Property}' at {error.Path}",
        ValidationErrorKind.PropertyRequired => $"missing required property '{error.Property}' at {error.Path}",
        ValidationErrorKind.NotInEnumeration => $"value for '{error.Property}' not in allowed enumeration at {error.Path}",
        _ => $"{error.Kind} on property '{error.Property}' at {error.Path}"
    };

    private static bool IsUnderJsonSchemasDir(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals(".json-schemas", StringComparison.OrdinalIgnoreCase));

    // Package JSON files map to a schema type by file name / containing folder, mirroring the
    // on-disk layout Template.Load itself reads (Schema/Domain/Template.cs: LoadTables reads
    // "Tables", LoadIndexedViews reads "Indexed Views", LoadMaterializedViews reads
    // "Materialized Views" — note the spaces, unlike the compact .schema file name segments).
    private static string MapFileToType(string jsonFilePath)
    {
        var fileName = Path.GetFileName(jsonFilePath);
        if (fileName.Equals("Product.json", StringComparison.OrdinalIgnoreCase)) return "products";
        if (fileName.Equals("Template.json", StringComparison.OrdinalIgnoreCase)) return "templates";

        var parent = Path.GetFileName(Path.GetDirectoryName(jsonFilePath) ?? "");
        if (parent.Equals("Tables", StringComparison.OrdinalIgnoreCase)) return "tables";
        if (parent.Equals("Indexed Views", StringComparison.OrdinalIgnoreCase)) return "indexedviews";
        if (parent.Equals("Materialized Views", StringComparison.OrdinalIgnoreCase)) return "materializedviews";
        return null;
    }

    // ---- Everything below mirrors Schema/Utility/RepositoryHelper.cs's private mapping.
    // Duplicated rather than exposed: SchemaQuench must not reference Schema's private members,
    // and this check must not modify Schema/. Keep these in lockstep with RepositoryHelper if the
    // domain model's platform-subclass or schema-file-naming scheme ever changes. ----

    private static Type GetTypeForSchemaFile(string fileName, Platform platform)
    {
        var objectPart = fileName.Split('.')[0];
        return (objectPart, platform) switch
        {
            ("products", _) => typeof(Product),
            ("templates", Platform.SqlServer) => typeof(SqlServerTemplate),
            ("templates", Platform.PostgreSQL) => typeof(PostgreSqlTemplate),
            ("templates", Platform.MySQL) => typeof(MySqlTemplate),
            ("tables", Platform.SqlServer) => typeof(SqlServerTable),
            ("tables", Platform.PostgreSQL) => typeof(PostgreSqlTable),
            ("tables", Platform.MySQL) => typeof(MySqlTable),
            ("indexedviews", Platform.SqlServer) => typeof(SqlServerIndexedView),
            ("materializedviews", Platform.PostgreSQL) => typeof(PostgreSqlMaterializedView),
            _ => throw new ArgumentException($"Unknown schema file mapping: {fileName} for platform {platform}")
        };
    }

    private static Func<Type, Type> PlatformElementResolver(Platform platform) => t =>
        t == typeof(Column) ? PlatformDeserializer.GetColumnType(platform)
        : t == typeof(Schema.Domain.Index) ? PlatformDeserializer.GetIndexType(platform)
        : t == typeof(ForeignKey) ? PlatformDeserializer.GetForeignKeyType(platform)
        : t == typeof(CheckConstraint) ? PlatformDeserializer.GetCheckConstraintType(platform)
        : t;
}

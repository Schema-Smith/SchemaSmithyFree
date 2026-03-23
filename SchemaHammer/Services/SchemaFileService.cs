// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaHammer.Services;

public class SchemaFileService : ISchemaFileService
{
    public int UpdateSchemaFiles(string productPath)
    {
        var file = FileWrapper.GetFromFactory();
        var directory = DirectoryWrapper.GetFromFactory();
        var schemaPath = Path.Combine(productPath, ".json-schemas");
        directory.CreateDirectory(schemaPath);

        file.WriteAllText(Path.Combine(schemaPath, "products.schema"),
            SchemaGenerator.GenerateSchema(typeof(Product)).ToString(Formatting.Indented));
        file.WriteAllText(Path.Combine(schemaPath, "templates.schema"),
            SchemaGenerator.GenerateSchema(typeof(Template)).ToString(Formatting.Indented));
        file.WriteAllText(Path.Combine(schemaPath, "tables.schema"),
            SchemaGenerator.GenerateSchema(typeof(Table)).ToString(Formatting.Indented));
        file.WriteAllText(Path.Combine(schemaPath, "indexedviews.schema"),
            SchemaGenerator.GenerateSchema(typeof(IndexedView)).ToString(Formatting.Indented));

        return 4;
    }
}

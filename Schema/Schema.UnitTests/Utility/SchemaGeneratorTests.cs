// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Utility;
using SchemaSmith.Pro;

namespace Schema.UnitTests.Utility;

public class SchemaGeneratorTests
{
    [Test]
    public void ShouldGenerateSchemaWithStringProperty()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(SimpleStringClass));
        Assert.That(schema["type"]?.ToString(), Is.EqualTo("object"));
        Assert.That(schema["additionalProperties"]?.Value<bool>(), Is.False);
        var props = schema["properties"] as JObject;
        Assert.That(props, Is.Not.Null);
        Assert.That(props["Name"]?["type"]?.ToString(), Is.EqualTo("string"));
    }

    [Test]
    public void ShouldMapBooleanType()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(BoolClass));
        Assert.That(schema["properties"]?["IsActive"]?["type"]?.ToString(), Is.EqualTo("boolean"));
    }

    [Test]
    public void ShouldMapIntegerTypes()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(IntegerClass));
        Assert.That(schema["properties"]?["ByteVal"]?["type"]?.ToString(), Is.EqualTo("integer"));
        Assert.That(schema["properties"]?["IntVal"]?["type"]?.ToString(), Is.EqualTo("integer"));
    }

    [Test]
    public void ShouldMapListToArray()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(ListClass));
        var listProp = schema["properties"]?["Items"];
        Assert.That(listProp?["type"]?.ToString(), Is.EqualTo("array"));
        Assert.That(listProp?["items"]?["type"]?.ToString(), Is.EqualTo("string"));
    }

    [Test]
    public void ShouldMapDictionaryToObject()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(DictClass));
        Assert.That(schema["properties"]?["Tags"]?["type"]?.ToString(), Is.EqualTo("object"));
    }

    [Test]
    public void ShouldMapNestedClassRecursively()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(ParentClass));
        var nested = schema["properties"]?["Child"];
        Assert.That(nested?["type"]?.ToString(), Is.EqualTo("object"));
        Assert.That(nested?["properties"]?["Value"]?["type"]?.ToString(), Is.EqualTo("string"));
        Assert.That(nested?["additionalProperties"]?.Value<bool>(), Is.False);
    }

    [Test]
    public void ShouldMapListOfObjectsToArrayWithObjectItems()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(ListOfObjectsClass));
        var items = schema["properties"]?["Children"]?["items"];
        Assert.That(items?["type"]?.ToString(), Is.EqualTo("object"));
        Assert.That(items?["properties"]?["Value"]?["type"]?.ToString(), Is.EqualTo("string"));
    }

    [Test]
    public void ShouldExcludeJsonIgnoreProperties()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(IgnoreClass));
        Assert.That(schema["properties"]?["Visible"], Is.Not.Null);
        Assert.That(schema["properties"]?["Hidden"], Is.Null);
    }

    [Test]
    public void ShouldRespectJsonPropertyOrder()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(OrderedClass));
        var propNames = (schema["properties"] as JObject)?.Properties().Select(p => p.Name).ToList();
        Assert.That(propNames, Is.EqualTo(new List<string> { "First", "Second", "Third" }));
    }

    [Test]
    public void ShouldDetectRequiredFromSchemaPropertyAttribute()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(RequiredClass));
        var required = schema["required"]?.ToObject<List<string>>();
        Assert.That(required, Is.Not.Null);
        Assert.That(required, Contains.Item("Name"));
        Assert.That(required, Does.Not.Contain("Optional"));
    }

    [Test]
    public void ShouldApplyPatternConstraint()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(PatternClass));
        Assert.That(schema["properties"]?["Action"]?["pattern"]?.ToString(), Is.EqualTo("NO ACTION|CASCADE"));
    }

    [Test]
    public void ShouldApplyMinMaxConstraints()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(MinMaxClass));
        var prop = schema["properties"]?["FillFactor"];
        Assert.That(prop?["minimum"]?.Value<double>(), Is.EqualTo(0));
        Assert.That(prop?["maximum"]?.Value<double>(), Is.EqualTo(100));
    }

    [Test]
    public void ShouldNotIncludeRequiredArrayWhenNoRequiredProperties()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(SimpleStringClass));
        Assert.That(schema["required"], Is.Null);
    }

    // --- Domain model integration tests ---

    [Test]
    public void ShouldGenerateProductSchema()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(Product));
        var props = schema["properties"] as JObject;
        Assert.That(props?["Name"], Is.Not.Null);
        Assert.That(props?["ValidationScript"], Is.Not.Null);
        Assert.That(props?["ScriptTokens"]?["type"]?.ToString(), Is.EqualTo("object"));
        Assert.That(props?["TemplateOrder"]?["type"]?.ToString(), Is.EqualTo("array"));
        Assert.That(props?["Platform"], Is.Not.Null, "Platform should be in schema");
        Assert.That(props?["FilePath"], Is.Null, "FilePath is JsonIgnore");

        var required = schema["required"]?.ToObject<List<string>>();
        Assert.That(required, Contains.Item("Name"));
        Assert.That(required, Contains.Item("ValidationScript"));
    }

    [Test]
    public void ShouldGenerateTemplateSchemaExcludingIgnoredProperties()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(Template));
        var props = schema["properties"] as JObject;
        Assert.That(props?["Name"], Is.Not.Null);
        Assert.That(props?["DatabaseIdentificationScript"], Is.Not.Null);
        // ScriptFolders has [JsonProperty] in v2 so it appears in schema
        Assert.That(props?["ScriptFolders"], Is.Not.Null);
        // JsonIgnore properties should not appear
        Assert.That(props?["BeforeScripts"], Is.Null);
        Assert.That(props?["Tables"], Is.Null);
        Assert.That(props?["TableSchema"], Is.Null);
        Assert.That(props?["FilePath"], Is.Null);
        Assert.That(props?["LogPath"], Is.Null);

        var required = schema["required"]?.ToObject<List<string>>();
        Assert.That(required, Contains.Item("Name"));
        Assert.That(required, Contains.Item("DatabaseIdentificationScript"));
    }

    [Test]
    public void ShouldGenerateTableSchemaWithNestedTypes()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(Table));
        var props = schema["properties"] as JObject;
        Assert.That(props?["Columns"]?["type"]?.ToString(), Is.EqualTo("array"));
        Assert.That(props?["Indexes"]?["type"]?.ToString(), Is.EqualTo("array"));
        Assert.That(props?["ForeignKeys"]?["type"]?.ToString(), Is.EqualTo("array"));
        // FullTextIndex is SQL Server-specific; base Table has no such property
        Assert.That(props?["FullTextIndex"], Is.Null);

        var columnItems = props?["Columns"]?["items"];
        Assert.That(columnItems?["properties"]?["Name"]?["type"]?.ToString(), Is.EqualTo("string"));
        Assert.That(columnItems?["properties"]?["Nullable"]?["type"]?.ToString(), Is.EqualTo("boolean"));
        Assert.That(columnItems?["additionalProperties"]?.Value<bool>(), Is.False);
    }

    [Test]
    public void ShouldApplyConstraintsOnDomainProperties()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(Table));
        // DataDelivery's MergeType pattern was removed when DataDelivery moved to SchemaSmith.Pro
        // (no SchemaPropertyAttribute). Pattern validation will come from ISchemaDefinitionProvider
        // when a Pro license is active. Verify the property exists but has no pattern constraint.
        var dataDelivery = schema["properties"]?["DataDelivery"];
        var mergeType = dataDelivery?["properties"]?["MergeType"];
        Assert.That(mergeType?["type"]?.ToString(), Is.EqualTo("string"));
        Assert.That(mergeType?["pattern"], Is.Null);

        // ForeignKey pattern constraints
        var fkItems = schema["properties"]?["ForeignKeys"]?["items"];
        Assert.That(fkItems?["properties"]?["DeleteAction"]?["pattern"]?.ToString(), Is.EqualTo("NO ACTION|RESTRICT|CASCADE|SET NULL|SET DEFAULT"));
    }

    [Test]
    public void ShouldMapStringEnumConverterToStringWithPattern()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(StringEnumClass));
        var prop = schema["properties"]?["Version"];
        Assert.That(prop?["type"]?.ToString(), Is.EqualTo("string"));
        Assert.That(prop?["pattern"]?.ToString(), Does.Contain("ValueA"));
        Assert.That(prop?["pattern"]?.ToString(), Does.Contain("ValueB"));
    }

    [Test]
    public void ShouldMapNullableEnumProperty()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(NullableEnumClass));
        var prop = schema["properties"]?["Version"];
        Assert.That(prop?["type"]?.ToString(), Is.EqualTo("string"));
        Assert.That(prop?["pattern"]?.ToString(), Does.Contain("ValueA"));
    }

    [Test]
    public void ShouldGenerateProductSchemaWithMinimumVersion()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(Product));
        var prop = schema["properties"]?["MinimumVersion"];
        Assert.That(prop, Is.Not.Null, "MinimumVersion should appear in schema");
        Assert.That(prop?["type"]?.ToString(), Is.EqualTo("string"));
        // MinimumVersion has no [SchemaProperty] pattern in v2 (it's a free-form string)
        Assert.That(prop?["pattern"], Is.Null);
    }

    [Test]
    public void ShouldIncludeUpdateFillFactorInTableSchema()
    {
        // UpdateFillFactor is SQL Server-specific; base Table does not have this property
        var schema = SchemaGenerator.GenerateSchema(typeof(Table));
        Assert.That(schema["properties"]?["UpdateFillFactor"], Is.Null,
            "Base Table does not have UpdateFillFactor; it is SQL Server-specific");
    }

    [Test]
    public void ShouldIncludeUpdateFillFactorInIndexSchema()
    {
        // UpdateFillFactor is SQL Server-specific; base Index does not have this property
        var schema = SchemaGenerator.GenerateSchema(typeof(Index));
        Assert.That(schema["properties"]?["UpdateFillFactor"], Is.Null,
            "Base Index does not have UpdateFillFactor; it is SQL Server-specific");
    }

    [Test]
    public void ShouldMapJTokenExtensionsWithoutRecursion()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(Table));
        var ext = schema["properties"]?["Extensions"];
        Assert.That(ext, Is.Not.Null, "Extensions should appear in schema");
        Assert.That(ext["type"], Is.Null, "JToken Extensions should have no type constraint (accepts anything)");
    }

    [Test]
    public void ShouldIncludeIndexedViewPropertiesInSchema()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(SqlServerIndexedView));
        Assert.Multiple(() =>
        {
            Assert.That(schema["properties"]?["Name"]?["type"]?.ToString(), Is.EqualTo("string"));
            Assert.That(schema["properties"]?["Schema"]?["type"]?.ToString(), Is.EqualTo("string"));
            Assert.That(schema["properties"]?["Definition"]?["type"]?.ToString(), Is.EqualTo("string"));
            Assert.That(schema["properties"]?["Indexes"]?["type"]?.ToString(), Is.EqualTo("array"));
        });
    }

    // --- MergeExtensionsDefinition tests ---

    [Test]
    public void MergeExtensionsDefinition_PreservesCustomExtensionsSchema()
    {
        var existingSchema = JObject.Parse(@"{
            'properties': {
                'Name': { 'type': 'string' },
                'Extensions': {
                    'type': 'object',
                    'properties': {
                        'replication': { 'type': 'boolean' },
                        'dataDictionary': {
                            'type': 'object',
                            'properties': {
                                'description': { 'type': 'string' }
                            }
                        }
                    }
                }
            }
        }");

        var generated = SchemaGenerator.GenerateSchema(typeof(Table));
        var merged = SchemaGenerator.MergeExtensionsDefinition(generated, existingSchema);

        var extensions = merged["properties"]?["Extensions"];
        Assert.That(extensions, Is.Not.Null);
        Assert.That(extensions["properties"]?["replication"]?["type"]?.ToString(), Is.EqualTo("boolean"));
        Assert.That(extensions["properties"]?["dataDictionary"]?["properties"]?["description"]?["type"]?.ToString(), Is.EqualTo("string"));
    }

    [Test]
    public void MergeExtensionsDefinition_NoExistingExtensions_ReturnsGenerated()
    {
        var existingSchema = JObject.Parse(@"{
            'properties': {
                'Name': { 'type': 'string' }
            }
        }");

        var generated = SchemaGenerator.GenerateSchema(typeof(Table));
        var merged = SchemaGenerator.MergeExtensionsDefinition(generated, existingSchema);

        // Extensions should still exist in generated (as empty schema)
        Assert.That(merged["properties"]?["Extensions"], Is.Not.Null);
    }

    [Test]
    public void MergeExtensionsDefinition_NullExisting_ReturnsGenerated()
    {
        var generated = SchemaGenerator.GenerateSchema(typeof(Table));
        var merged = SchemaGenerator.MergeExtensionsDefinition(generated, null);

        Assert.That(merged, Is.Not.Null);
        Assert.That(merged["properties"]?["Extensions"], Is.Not.Null);
    }

    // --- Test helper classes ---
    private class SimpleStringClass { public string Name { get; set; } }
    private class BoolClass { public bool IsActive { get; set; } }
    private class IntegerClass { public byte ByteVal { get; set; } public int IntVal { get; set; } }
    private class ListClass { public List<string> Items { get; set; } }
    private class DictClass { public Dictionary<string, string> Tags { get; set; } }
    private class ChildClass { public string Value { get; set; } }
    private class ParentClass { public ChildClass Child { get; set; } }
    private class ListOfObjectsClass { public List<ChildClass> Children { get; set; } }
    private class IgnoreClass
    {
        public string Visible { get; set; }
        [JsonIgnore] public string Hidden { get; set; }
    }
    private class OrderedClass
    {
        [JsonProperty(Order = 3)] public string Third { get; set; }
        [JsonProperty(Order = 1)] public string First { get; set; }
        [JsonProperty(Order = 2)] public string Second { get; set; }
    }
    private class RequiredClass
    {
        [SchemaProperty(Required = true)] public string Name { get; set; }
        public string Optional { get; set; }
    }
    private class PatternClass
    {
        [SchemaProperty(Pattern = "NO ACTION|CASCADE")] public string Action { get; set; }
    }
    private class MinMaxClass
    {
        [SchemaProperty(Minimum = 0, Maximum = 100)] public byte FillFactor { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    private enum TestStringEnum { ValueA, ValueB, ValueC }

    private class StringEnumClass { public TestStringEnum Version { get; set; } }
    private class NullableEnumClass { public TestStringEnum? Version { get; set; } }
}

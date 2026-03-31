// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Domain;

namespace Schema.UnitTests.Domain;

[TestFixture]
public class ExtensionsSerializationTests
{
    private static readonly JsonSerializerSettings WriteSettings = new()
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented
    };

    [Test]
    public void Table_RoundTrip_SimpleObject()
    {
        var json = """
        {
            "Name": "Users",
            "Columns": [{ "Name": "Id", "DataType": "INT" }],
            "Extensions": { "Team": "Platform" }
        }
        """;

        var table = JsonConvert.DeserializeObject<Table>(json);
        Assert.That(table.Extensions, Is.Not.Null);
        Assert.That(table.Extensions["Team"]?.ToString(), Is.EqualTo("Platform"));

        var reserialized = JsonConvert.SerializeObject(table, WriteSettings);
        var roundTripped = JsonConvert.DeserializeObject<Table>(reserialized);
        Assert.That(roundTripped.Extensions["Team"]?.ToString(), Is.EqualTo("Platform"));
    }

    [Test]
    public void Table_RoundTrip_NestedObject()
    {
        var json = """
        {
            "Name": "Audit",
            "Columns": [{ "Name": "Id", "DataType": "INT" }],
            "Extensions": { "Compliance": { "Level": "SOC2", "ApprovedBy": "Jane" } }
        }
        """;

        var table = JsonConvert.DeserializeObject<Table>(json);
        var compliance = table.Extensions["Compliance"] as JObject;
        Assert.That(compliance, Is.Not.Null);
        Assert.That(compliance["Level"]?.ToString(), Is.EqualTo("SOC2"));
        Assert.That(compliance["ApprovedBy"]?.ToString(), Is.EqualTo("Jane"));

        var reserialized = JsonConvert.SerializeObject(table, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Table>(reserialized);
        var rtCompliance = rt.Extensions["Compliance"] as JObject;
        Assert.That(rtCompliance["Level"]?.ToString(), Is.EqualTo("SOC2"));
    }

    [Test]
    public void Table_RoundTrip_Array()
    {
        var json = """
        {
            "Name": "Tags",
            "Columns": [{ "Name": "Id", "DataType": "INT" }],
            "Extensions": { "Tags": ["critical", "audit"] }
        }
        """;

        var table = JsonConvert.DeserializeObject<Table>(json);
        var tags = table.Extensions["Tags"] as JArray;
        Assert.That(tags, Has.Count.EqualTo(2));
        Assert.That(tags[0]?.ToString(), Is.EqualTo("critical"));

        var reserialized = JsonConvert.SerializeObject(table, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Table>(reserialized);
        var rtTags = rt.Extensions["Tags"] as JArray;
        Assert.That(rtTags, Has.Count.EqualTo(2));
    }

    [Test]
    public void Table_RoundTrip_Primitive()
    {
        var jsonInt = """
        {
            "Name": "T1",
            "Columns": [{ "Name": "Id", "DataType": "INT" }],
            "Extensions": 42
        }
        """;

        var table = JsonConvert.DeserializeObject<Table>(jsonInt);
        Assert.That(table.Extensions.Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(table.Extensions.Value<int>(), Is.EqualTo(42));

        var reserialized = JsonConvert.SerializeObject(table, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Table>(reserialized);
        Assert.That(rt.Extensions.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void Table_RoundTrip_MixedComplexStructure()
    {
        var json = """
        {
            "Name": "Mixed",
            "Columns": [{ "Name": "Id", "DataType": "INT" }],
            "Extensions": { "Tags": ["a","b"], "Meta": { "Key": 1 }, "Flag": true }
        }
        """;

        var table = JsonConvert.DeserializeObject<Table>(json);
        var ext = table.Extensions as JObject;
        Assert.That(ext, Is.Not.Null);
        Assert.That((ext["Tags"] as JArray), Has.Count.EqualTo(2));
        Assert.That(ext["Meta"]["Key"]?.Value<int>(), Is.EqualTo(1));
        Assert.That(ext["Flag"]?.Value<bool>(), Is.True);

        var reserialized = JsonConvert.SerializeObject(table, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Table>(reserialized);
        var rtExt = rt.Extensions as JObject;
        Assert.That((rtExt["Tags"] as JArray), Has.Count.EqualTo(2));
        Assert.That(rtExt["Meta"]["Key"]?.Value<int>(), Is.EqualTo(1));
        Assert.That(rtExt["Flag"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void Table_NullExtensions_OmittedFromJson()
    {
        var table = new Table { Name = "T1", Columns = [new Column { Name = "Id", DataType = "INT" }] };
        var json = JsonConvert.SerializeObject(table, WriteSettings);
        Assert.That(json, Does.Not.Contain("Extensions"));
    }

    [Test]
    public void Table_MissingExtensionsInJson_DeserializesToNull()
    {
        var json = """{ "Name": "T1", "Columns": [{ "Name": "Id", "DataType": "INT" }] }""";
        var table = JsonConvert.DeserializeObject<Table>(json);
        Assert.That(table.Extensions, Is.Null);
    }

    [Test]
    public void Column_RoundTrip_Extensions()
    {
        var json = """{ "Name": "Email", "DataType": "NVARCHAR(255)", "Extensions": { "PII": true } }""";
        var col = JsonConvert.DeserializeObject<Column>(json);
        Assert.That((col.Extensions as JObject)?["PII"]?.Value<bool>(), Is.True);

        var reserialized = JsonConvert.SerializeObject(col, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Column>(reserialized);
        Assert.That((rt.Extensions as JObject)?["PII"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void Index_RoundTrip_Extensions()
    {
        var json = """{ "Name": "IX_Test", "IndexColumns": "Col1", "Extensions": { "Purpose": "perf" } }""";
        var idx = JsonConvert.DeserializeObject<Index>(json);
        Assert.That((idx.Extensions as JObject)?["Purpose"]?.ToString(), Is.EqualTo("perf"));

        var reserialized = JsonConvert.SerializeObject(idx, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Index>(reserialized);
        Assert.That((rt.Extensions as JObject)?["Purpose"]?.ToString(), Is.EqualTo("perf"));
    }

    [Test]
    public void ForeignKey_RoundTrip_Extensions()
    {
        var json = """{ "Name": "FK_Test", "Columns": "UserId", "RelatedTable": "Users", "RelatedColumns": "Id", "Extensions": { "Audited": true } }""";
        var fk = JsonConvert.DeserializeObject<ForeignKey>(json);
        Assert.That((fk.Extensions as JObject)?["Audited"]?.Value<bool>(), Is.True);

        var reserialized = JsonConvert.SerializeObject(fk, WriteSettings);
        var rt = JsonConvert.DeserializeObject<ForeignKey>(reserialized);
        Assert.That((rt.Extensions as JObject)?["Audited"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void CheckConstraint_RoundTrip_Extensions()
    {
        var json = """{ "Name": "CK_Test", "Expression": "[Col] > 0", "Extensions": { "Owner": "DBA" } }""";
        var ck = JsonConvert.DeserializeObject<CheckConstraint>(json);
        Assert.That((ck.Extensions as JObject)?["Owner"]?.ToString(), Is.EqualTo("DBA"));

        var reserialized = JsonConvert.SerializeObject(ck, WriteSettings);
        var rt = JsonConvert.DeserializeObject<CheckConstraint>(reserialized);
        Assert.That((rt.Extensions as JObject)?["Owner"]?.ToString(), Is.EqualTo("DBA"));
    }

    [Test]
    public void Statistic_RoundTrip_Extensions()
    {
        var json = """{ "Name": "ST_Test", "Columns": "Col1", "Extensions": { "Auto": false } }""";
        var stat = JsonConvert.DeserializeObject<Statistic>(json);
        Assert.That((stat.Extensions as JObject)?["Auto"]?.Value<bool>(), Is.False);

        var reserialized = JsonConvert.SerializeObject(stat, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Statistic>(reserialized);
        Assert.That((rt.Extensions as JObject)?["Auto"]?.Value<bool>(), Is.False);
    }

    [Test]
    public void FullTextIndex_RoundTrip_Extensions()
    {
        var json = """{ "FullTextCatalog": "FTC", "KeyIndex": "PK_Test", "Columns": "Col1", "Extensions": { "Lang": "en" } }""";
        var ft = JsonConvert.DeserializeObject<FullTextIndex>(json);
        Assert.That((ft.Extensions as JObject)?["Lang"]?.ToString(), Is.EqualTo("en"));

        var reserialized = JsonConvert.SerializeObject(ft, WriteSettings);
        var rt = JsonConvert.DeserializeObject<FullTextIndex>(reserialized);
        Assert.That((rt.Extensions as JObject)?["Lang"]?.ToString(), Is.EqualTo("en"));
    }

    [Test]
    public void XmlIndex_RoundTrip_Extensions()
    {
        var json = """{ "Name": "XI_Test", "Column": "XmlCol", "Extensions": { "Type": "content" } }""";
        var xi = JsonConvert.DeserializeObject<XmlIndex>(json);
        Assert.That((xi.Extensions as JObject)?["Type"]?.ToString(), Is.EqualTo("content"));

        var reserialized = JsonConvert.SerializeObject(xi, WriteSettings);
        var rt = JsonConvert.DeserializeObject<XmlIndex>(reserialized);
        Assert.That((rt.Extensions as JObject)?["Type"]?.ToString(), Is.EqualTo("content"));
    }

    [Test]
    public void IndexedView_RoundTrip_Extensions()
    {
        var json = """{ "Name": "IV_Test", "Definition": "SELECT 1", "Extensions": { "Refresh": "daily" } }""";
        var iv = JsonConvert.DeserializeObject<IndexedView>(json);
        Assert.That((iv.Extensions as JObject)?["Refresh"]?.ToString(), Is.EqualTo("daily"));

        var reserialized = JsonConvert.SerializeObject(iv, WriteSettings);
        var rt = JsonConvert.DeserializeObject<IndexedView>(reserialized);
        Assert.That((rt.Extensions as JObject)?["Refresh"]?.ToString(), Is.EqualTo("daily"));
    }

    [Test]
    public void FullTable_RoundTrip_ExtensionsAtAllLevels()
    {
        var json = """
        {
            "Name": "Orders",
            "Columns": [{ "Name": "Id", "DataType": "INT", "Extensions": { "PII": false } }],
            "Indexes": [{ "Name": "PK_Orders", "IndexColumns": "Id", "PrimaryKey": true, "Extensions": { "Perf": "critical" } }],
            "ForeignKeys": [{ "Name": "FK_User", "Columns": "UserId", "RelatedTable": "Users", "RelatedColumns": "Id", "Extensions": { "Cascade": true } }],
            "CheckConstraints": [{ "Name": "CK_Amt", "Expression": "[Amt] > 0", "Extensions": { "Rule": "biz" } }],
            "Statistics": [{ "Name": "ST_Col", "Columns": "Col1", "Extensions": { "Custom": true } }],
            "Extensions": { "TableOwner": "OrdersTeam" }
        }
        """;

        var table = JsonConvert.DeserializeObject<Table>(json);
        Assert.That((table.Extensions as JObject)?["TableOwner"]?.ToString(), Is.EqualTo("OrdersTeam"));
        Assert.That((table.Columns[0].Extensions as JObject)?["PII"]?.Value<bool>(), Is.False);
        Assert.That((table.Indexes[0].Extensions as JObject)?["Perf"]?.ToString(), Is.EqualTo("critical"));
        Assert.That((table.ForeignKeys[0].Extensions as JObject)?["Cascade"]?.Value<bool>(), Is.True);
        Assert.That((table.CheckConstraints[0].Extensions as JObject)?["Rule"]?.ToString(), Is.EqualTo("biz"));
        Assert.That((table.Statistics[0].Extensions as JObject)?["Custom"]?.Value<bool>(), Is.True);

        var reserialized = JsonConvert.SerializeObject(table, WriteSettings);
        var rt = JsonConvert.DeserializeObject<Table>(reserialized);
        Assert.That((rt.Extensions as JObject)?["TableOwner"]?.ToString(), Is.EqualTo("OrdersTeam"));
        Assert.That((rt.Columns[0].Extensions as JObject)?["PII"]?.Value<bool>(), Is.False);
        Assert.That((rt.Indexes[0].Extensions as JObject)?["Perf"]?.ToString(), Is.EqualTo("critical"));
        Assert.That((rt.ForeignKeys[0].Extensions as JObject)?["Cascade"]?.Value<bool>(), Is.True);
        Assert.That((rt.CheckConstraints[0].Extensions as JObject)?["Rule"]?.ToString(), Is.EqualTo("biz"));
        Assert.That((rt.Statistics[0].Extensions as JObject)?["Custom"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void Extensions_AppearsInSerializedTableMetadata()
    {
        var table = new Table
        {
            Name = "T1",
            Columns = [new Column { Name = "Id", DataType = "INT", Extensions = JToken.Parse("""{ "PII": true }""") }],
            Extensions = JToken.Parse("""{ "Team": "Platform" }""")
        };

        var serialized = JsonConvert.SerializeObject(new List<Table> { table }, Formatting.Indented);
        Assert.That(serialized, Does.Contain("\"Extensions\""));
        Assert.That(serialized, Does.Contain("\"Team\""));
        Assert.That(serialized, Does.Contain("\"PII\""));
    }
}

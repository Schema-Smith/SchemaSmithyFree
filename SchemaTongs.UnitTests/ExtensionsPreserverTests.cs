// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json.Linq;
using Schema.Domain;
using SchemaTongs;

namespace SchemaTongs.UnitTests;

[TestFixture]
public class ExtensionsPreserverTests
{
    [Test]
    public void PreserveTableExtensions_CarriesForwardTableLevelExtensions()
    {
        var original = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            Extensions = JToken.Parse("""{ "Team": "Platform" }""")
        };
        var extracted = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.Extensions as JObject)?["Team"]?.ToString(), Is.EqualTo("Platform"));
    }

    [Test]
    public void PreserveTableExtensions_CarriesForwardColumnExtensions()
    {
        var original = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Email", DataType = "NVARCHAR(255)", Extensions = JToken.Parse("""{ "PII": true }""") }]
        };
        var extracted = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Email", DataType = "NVARCHAR(255)" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.Columns[0].Extensions as JObject)?["PII"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void PreserveTableExtensions_MatchesBracketStrippedNames()
    {
        var original = new Table
        {
            Name = "[Users]", Schema = "[dbo]",
            Columns = [new Column { Name = "[Email]", DataType = "NVARCHAR(255)", Extensions = JToken.Parse("""{ "PII": true }""") }]
        };
        var extracted = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Email", DataType = "NVARCHAR(255)" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.Columns[0].Extensions as JObject)?["PII"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void PreserveTableExtensions_DropsExtensionsForRemovedComponents()
    {
        var original = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [
                new Column { Name = "Id", DataType = "INT" },
                new Column { Name = "Deleted", DataType = "BIT", Extensions = JToken.Parse("""{ "Legacy": true }""") }
            ]
        };
        var extracted = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That(extracted.Columns[0].Extensions, Is.Null);
    }

    [Test]
    public void PreserveTableExtensions_NullExtensionsForNewComponents()
    {
        var original = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }]
        };
        var extracted = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [
                new Column { Name = "Id", DataType = "INT" },
                new Column { Name = "NewCol", DataType = "NVARCHAR(100)" }
            ]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That(extracted.Columns[1].Extensions, Is.Null);
    }

    [Test]
    public void PreserveTableExtensions_MatchesByOldName()
    {
        var original = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "EmailAddr", DataType = "NVARCHAR(255)", Extensions = JToken.Parse("""{ "PII": true }""") }]
        };
        var extracted = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Email", DataType = "NVARCHAR(255)", OldName = "EmailAddr" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.Columns[0].Extensions as JObject)?["PII"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void PreserveTableExtensions_PreservesIndexExtensions()
    {
        var original = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            Indexes = [new Index { Name = "IX_Email", IndexColumns = "Email", Extensions = JToken.Parse("""{ "Perf": "critical" }""") }]
        };
        var extracted = new Table
        {
            Name = "Users", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            Indexes = [new Index { Name = "IX_Email", IndexColumns = "Email" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.Indexes[0].Extensions as JObject)?["Perf"]?.ToString(), Is.EqualTo("critical"));
    }

    [Test]
    public void PreserveTableExtensions_PreservesForeignKeyExtensions()
    {
        var original = new Table
        {
            Name = "Orders", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            ForeignKeys = [new ForeignKey { Name = "FK_User", Columns = "UserId", RelatedTable = "Users", RelatedColumns = "Id", Extensions = JToken.Parse("""{ "Validated": true }""") }]
        };
        var extracted = new Table
        {
            Name = "Orders", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            ForeignKeys = [new ForeignKey { Name = "FK_User", Columns = "UserId", RelatedTable = "Users", RelatedColumns = "Id" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.ForeignKeys[0].Extensions as JObject)?["Validated"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void PreserveTableExtensions_PreservesCheckConstraintExtensions()
    {
        var original = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            CheckConstraints = [new CheckConstraint { Name = "CK_Amt", Expression = "[Amt] > 0", Extensions = JToken.Parse("""{ "Rule": "biz" }""") }]
        };
        var extracted = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            CheckConstraints = [new CheckConstraint { Name = "CK_Amt", Expression = "[Amt] > 0" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.CheckConstraints[0].Extensions as JObject)?["Rule"]?.ToString(), Is.EqualTo("biz"));
    }

    [Test]
    public void PreserveTableExtensions_PreservesStatisticExtensions()
    {
        var original = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            Statistics = [new Statistic { Name = "ST_Col", Columns = "Col1", Extensions = JToken.Parse("""{ "Custom": true }""") }]
        };
        var extracted = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            Statistics = [new Statistic { Name = "ST_Col", Columns = "Col1" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.Statistics[0].Extensions as JObject)?["Custom"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void PreserveTableExtensions_PreservesFullTextIndexExtensions()
    {
        var original = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            FullTextIndex = new FullTextIndex { FullTextCatalog = "FTC", KeyIndex = "PK", Columns = "Col1", Extensions = JToken.Parse("""{ "Lang": "en" }""") }
        };
        var extracted = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            FullTextIndex = new FullTextIndex { FullTextCatalog = "FTC", KeyIndex = "PK", Columns = "Col1" }
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.FullTextIndex.Extensions as JObject)?["Lang"]?.ToString(), Is.EqualTo("en"));
    }

    [Test]
    public void PreserveTableExtensions_PreservesXmlIndexExtensions()
    {
        var original = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            XmlIndexes = [new XmlIndex { Name = "XI_Data", Column = "XmlCol", Extensions = JToken.Parse("""{ "Type": "content" }""") }]
        };
        var extracted = new Table
        {
            Name = "T1", Schema = "dbo",
            Columns = [new Column { Name = "Id", DataType = "INT" }],
            XmlIndexes = [new XmlIndex { Name = "XI_Data", Column = "XmlCol" }]
        };

        ExtensionsPreserver.PreserveTableExtensions(original, extracted);

        Assert.That((extracted.XmlIndexes[0].Extensions as JObject)?["Type"]?.ToString(), Is.EqualTo("content"));
    }

    [Test]
    public void PreserveIndexedViewExtensions_CarriesForward()
    {
        var original = new IndexedView
        {
            Name = "vw_Summary", Schema = "dbo", Definition = "SELECT 1",
            Indexes = [new Index { Name = "IX_1", IndexColumns = "Col1", Extensions = JToken.Parse("""{ "Perf": "hot" }""") }],
            Extensions = JToken.Parse("""{ "Refresh": "daily" }""")
        };
        var extracted = new IndexedView
        {
            Name = "vw_Summary", Schema = "dbo", Definition = "SELECT 1",
            Indexes = [new Index { Name = "IX_1", IndexColumns = "Col1" }]
        };

        ExtensionsPreserver.PreserveIndexedViewExtensions(original, extracted);

        Assert.That((extracted.Extensions as JObject)?["Refresh"]?.ToString(), Is.EqualTo("daily"));
        Assert.That((extracted.Indexes[0].Extensions as JObject)?["Perf"]?.ToString(), Is.EqualTo("hot"));
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Domain.PostgreSQL;
using Schema.Domain.MySQL;
using Schema.Utility;
using Schema.Delivery;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class ImportTableHelperTests
{
    [Test]
    public void PreserveDataDeliveryAndCustomProperties_CopiesDataDeliveryFields()
    {
        var original = new SqlServerTable
        {
            Name = "Orders",
            DataDelivery = new DataDelivery { ContentFile = "orders.csv", MergeType = "Insert/Update", MatchColumns = "OrderId", MergeFilter = "Active = 1", MergeDisableTriggers = true },
            OldName = "OldOrders"
        };

        var newTable = new SqlServerTable { Name = "Orders" };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newTable.DataDelivery.ContentFile, Is.EqualTo("orders.csv"));
        Assert.That(newTable.DataDelivery.MergeType, Is.EqualTo("Insert/Update"));
        Assert.That(newTable.DataDelivery.MatchColumns, Is.EqualTo("OrderId"));
        Assert.That(newTable.DataDelivery.MergeFilter, Is.EqualTo("Active = 1"));
        Assert.That(newTable.DataDelivery.MergeDisableTriggers, Is.True);
        Assert.That(newTable.OldName, Is.EqualTo("OldOrders"));
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_DefaultsMergeTypeToNone()
    {
        var original = new SqlServerTable { Name = "Orders", DataDelivery = new DataDelivery { MergeType = null } };
        var newTable = new SqlServerTable { Name = "Orders" };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newTable.DataDelivery.MergeType, Is.EqualTo("None"));
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_CopiesDynamicProperties()
    {
        var original = new SqlServerTable { Name = "Orders" };
        original.Extensions = new JObject { ["CustomProp"] = "CustomValue" };

        var newTable = new SqlServerTable { Name = "Orders" };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newTable.Extensions?["CustomProp"]?.ToString(), Is.EqualTo("CustomValue"));
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_CopiesColumnDynamicProperties()
    {
        var originalCol = new Column { Name = "OrderId" };
        originalCol.Extensions = new JObject { ["IsAudited"] = true };

        var original = new SqlServerTable { Name = "Orders", Columns = { originalCol } };
        var newCol = new Column { Name = "OrderId" };
        var newTable = new SqlServerTable { Name = "Orders", Columns = { newCol } };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newCol.Extensions?["IsAudited"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_MatchesByOldName()
    {
        var originalCol = new Column { Name = "OldColName", OldName = "OldColName" };
        originalCol.Extensions = new JObject { ["Custom"] = "value" };

        var original = new SqlServerTable { Name = "Orders", Columns = { originalCol } };

        // The new column's current name matches the original's OldName
        var newCol = new Column { Name = "OldColName" };
        var newTable = new SqlServerTable { Name = "Orders", Columns = { newCol } };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newCol.Extensions?["Custom"]?.ToString(), Is.EqualTo("value"));
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_PostgreSQL_CopiesPlatformSpecific()
    {
        var original = new PostgreSqlTable
        {
            Name = "orders",
            DataDelivery = new PostgreSqlDataDelivery { MergeDisableRules = true, MergeUpdateDescendents = true }
        };

        var newTable = new PostgreSqlTable { Name = "orders" };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newTable.DataDelivery.MergeDisableRules, Is.True);
        Assert.That(newTable.DataDelivery.MergeUpdateDescendents, Is.True);
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_MySQL_CopiesFullTextIndexDynamicProps()
    {
        var originalFti = new Schema.Domain.MySQL.FullTextIndex { Name = "fti_content" };
        originalFti.Extensions = new JObject { ["CustomParser"] = "ngram" };

        var original = new MySqlTable { Name = "articles", FullTextIndexes = { originalFti } };

        var newFti = new Schema.Domain.MySQL.FullTextIndex { Name = "fti_content" };
        var newTable = new MySqlTable { Name = "articles", FullTextIndexes = { newFti } };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newFti.Extensions?["CustomParser"]?.ToString(), Is.EqualTo("ngram"));
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_SqlServer_CopiesXmlIndexDynamicProps()
    {
        var originalXml = new XmlIndex { Name = "XI_Data" };
        originalXml.Extensions = new JObject { ["CustomFlag"] = "yes" };

        var original = new SqlServerTable { Name = "Docs", XmlIndexes = { originalXml } };

        var newXml = new XmlIndex { Name = "XI_Data" };
        var newTable = new SqlServerTable { Name = "Docs", XmlIndexes = { newXml } };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newXml.Extensions?["CustomFlag"]?.ToString(), Is.EqualTo("yes"));
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_HandlesNullContentFile()
    {
        var original = new SqlServerTable { Name = "Orders", DataDelivery = null };
        var newTable = new SqlServerTable { Name = "Orders" };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newTable.DataDelivery, Is.Null);
    }

    [Test]
    public void PreserveDataDeliveryAndCustomProperties_MatchesColumnsIgnoringQuotes()
    {
        var originalCol = new Column { Name = "[OrderId]" };
        originalCol.Extensions = new JObject { ["IsKey"] = true };

        var original = new SqlServerTable { Name = "Orders", Columns = { originalCol } };

        // New column with brackets
        var newCol = new Column { Name = "[OrderId]" };
        var newTable = new SqlServerTable { Name = "Orders", Columns = { newCol } };

        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(newTable, original);

        Assert.That(newCol.Extensions?["IsKey"]?.Value<bool>(), Is.True);
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests.Domain;

[TestFixture]
public class TableDataDeliveryListTests
{
    [Test]
    public void BareObject_ParsesToOneElementList_AndReserializesAsBareObject()
    {
        var json = "{\"Name\":\"[dbo].[Foo]\",\"DataDelivery\":{\"ContentFile\":\"d.json\",\"MergeType\":\"Insert\"}}";
        var table = JsonConvert.DeserializeObject<Table>(json);
        Assert.That(table.DataDelivery, Has.Count.EqualTo(1));

        var reserialized = JsonConvert.SerializeObject(table);
        Assert.That(reserialized, Does.Contain("\"DataDelivery\":{"));
        Assert.That(reserialized, Does.Not.Contain("\"DataDelivery\":["));
    }

    [Test]
    public void NoDataDelivery_OmitsPropertyOnSerialize()
    {
        var table = new Table { Name = "[dbo].[Foo]" };
        Assert.That(JsonConvert.SerializeObject(table), Does.Not.Contain("DataDelivery"));
    }
}

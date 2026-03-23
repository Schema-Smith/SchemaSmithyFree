// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using System.Collections.Generic;

namespace Schema.UnitTests;

public class IndexedViewTests
{
    [Test]
    public void ShouldDefaultSchemaToDatabase()
    {
        var view = new IndexedView();
        Assert.That(view.Schema, Is.EqualTo("dbo"));
    }

    [Test]
    public void ShouldDefaultToEmptyIndexes()
    {
        var view = new IndexedView();
        Assert.That(view.Indexes, Is.Not.Null);
        Assert.That(view.Indexes, Is.Empty);
    }

    [Test]
    public void ShouldSerializeAndDeserialize()
    {
        var view = new IndexedView
        {
            Name = "vw_ActiveOrders",
            Schema = "sales",
            Definition = "SELECT o.OrderId, o.Status FROM sales.Orders o WHERE o.Status = 'Active'",
            Indexes =
            [
                new Index { Name = "CIX_vw_ActiveOrders", Clustered = true, Unique = true, IndexColumns = "OrderId" }
            ]
        };
        var json = JsonConvert.SerializeObject(view, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<IndexedView>(json);
        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("vw_ActiveOrders"));
            Assert.That(deserialized.Schema, Is.EqualTo("sales"));
            Assert.That(deserialized.Definition, Does.Contain("SELECT"));
            Assert.That(deserialized.Indexes, Has.Count.EqualTo(1));
            Assert.That(deserialized.Indexes[0].Clustered, Is.True);
        });
    }

    [Test]
    public void ShouldDeserializeWithDefaults()
    {
        var json = """{"Name": "vw_Test", "Definition": "SELECT 1 AS Id"}""";
        var view = JsonConvert.DeserializeObject<IndexedView>(json);
        Assert.Multiple(() =>
        {
            Assert.That(view!.Schema, Is.EqualTo("dbo"));
            Assert.That(view.Indexes, Is.Empty);
        });
    }
}

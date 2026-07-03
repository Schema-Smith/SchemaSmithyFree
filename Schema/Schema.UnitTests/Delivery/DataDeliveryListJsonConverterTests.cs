// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Newtonsoft.Json;
using Schema.Delivery;

namespace Schema.UnitTests.Delivery;

[TestFixture]
public class DataDeliveryListJsonConverterTests
{
    private class Holder
    {
        [JsonConverter(typeof(DataDeliveryListJsonConverter))]
        public List<DataDelivery> DataDelivery { get; set; } = [];
        public bool ShouldSerializeDataDelivery() => DataDelivery is { Count: > 0 };
    }

    [Test]
    public void SingleObjectJson_DeserializesAsOneElementList()
    {
        var h = JsonConvert.DeserializeObject<Holder>(
            "{\"DataDelivery\":{\"ContentFile\":\"d.json\",\"MergeType\":\"Insert\"}}");
        Assert.That(h.DataDelivery, Has.Count.EqualTo(1));
        Assert.That(h.DataDelivery[0].ContentFile, Is.EqualTo("d.json"));
    }

    [Test]
    public void ArrayJson_DeserializesAsVariantList()
    {
        var h = JsonConvert.DeserializeObject<Holder>(
            "{\"DataDelivery\":[" +
            "{\"ContentFile\":\"dev.json\",\"MergeType\":\"Insert\",\"ShouldApplyExpression\":\"1=1\"}," +
            "{\"ContentFile\":\"prod.json\",\"MergeType\":\"Insert\",\"ShouldApplyExpression\":\"1=0\"}]}");
        Assert.That(h.DataDelivery, Has.Count.EqualTo(2));
    }

    [Test]
    public void MultipleVariantsMissingExpressions_ThrowsAtLoad()
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Holder>(
            "{\"DataDelivery\":[" +
            "{\"ContentFile\":\"a.json\",\"MergeType\":\"Insert\"}," +
            "{\"ContentFile\":\"b.json\",\"MergeType\":\"Insert\"}]}"));
    }

    [Test]
    public void SingleDelivery_SerializesAsBareObject()
    {
        var h = new Holder { DataDelivery = [new DataDelivery { ContentFile = "d.json", MergeType = "Insert" }] };
        var json = JsonConvert.SerializeObject(h);
        Assert.That(json, Does.Contain("\"DataDelivery\":{"));
        Assert.That(json, Does.Not.Contain("\"DataDelivery\":["));
    }

    [Test]
    public void TwoDeliveries_SerializeAsArray()
    {
        var h = new Holder
        {
            DataDelivery =
            [
                new DataDelivery { ContentFile = "a.json", MergeType = "Insert", ShouldApplyExpression = "1=1" },
                new DataDelivery { ContentFile = "b.json", MergeType = "Insert", ShouldApplyExpression = "1=0" }
            ]
        };
        var json = JsonConvert.SerializeObject(h);
        Assert.That(json, Does.Contain("\"DataDelivery\":["));
    }

    [Test]
    public void EmptyList_OmittedFromJson()
    {
        var json = JsonConvert.SerializeObject(new Holder());
        Assert.That(json, Does.Not.Contain("DataDelivery"));
    }

    [Test]
    public void NullVariantEntry_ThrowsAtLoad()
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Holder>(
            "{\"DataDelivery\":[{\"ContentFile\":\"a.json\",\"MergeType\":\"Insert\",\"ShouldApplyExpression\":\"1=1\"},null]}"));
    }
}

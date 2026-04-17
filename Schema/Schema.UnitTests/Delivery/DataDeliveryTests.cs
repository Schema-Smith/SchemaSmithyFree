// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Delivery;

namespace Schema.UnitTests.Delivery;

public class DataDeliveryTests
{
    [Test]
    public void DataDelivery_Defaults_AreNullOrFalse()
    {
        var dd = new DataDelivery();
        Assert.That(dd.ContentFile, Is.Null);
        Assert.That(dd.MergeType, Is.Null);
        Assert.That(dd.MatchColumns, Is.Null);
        Assert.That(dd.MergeFilter, Is.Null);
        Assert.That(dd.MergeDisableTriggers, Is.False);
    }

    [Test]
    public void DataDelivery_RoundTrips_ViaJson()
    {
        var dd = new DataDelivery
        {
            ContentFile = "data/customers.json",
            MergeType = "Insert/Update",
            MatchColumns = "CustomerId",
            MergeFilter = "IsActive = 1",
            MergeDisableTriggers = true
        };
        var json = JsonConvert.SerializeObject(dd);
        var roundTripped = JsonConvert.DeserializeObject<DataDelivery>(json);
        Assert.That(roundTripped.ContentFile, Is.EqualTo("data/customers.json"));
        Assert.That(roundTripped.MergeType, Is.EqualTo("Insert/Update"));
        Assert.That(roundTripped.MatchColumns, Is.EqualTo("CustomerId"));
        Assert.That(roundTripped.MergeFilter, Is.EqualTo("IsActive = 1"));
        Assert.That(roundTripped.MergeDisableTriggers, Is.True);
    }
}

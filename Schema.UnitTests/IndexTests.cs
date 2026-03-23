// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests;

public class IndexTests
{
    [Test]
    public void ShouldSerializeUpdateFillFactor()
    {
        var index = new Index { Name = "IX_Test", IndexColumns = "Col1", UpdateFillFactor = true };
        var json = JsonConvert.SerializeObject(index);
        Assert.That(json, Contains.Substring("\"UpdateFillFactor\":true"));
    }

    [Test]
    public void ShouldDeserializeUpdateFillFactorDefaultsToFalse()
    {
        var json = "{\"Name\":\"IX_Test\",\"IndexColumns\":\"Col1\"}";
        var index = JsonConvert.DeserializeObject<Index>(json);
        Assert.That(index!.UpdateFillFactor, Is.False);
    }

    [Test]
    public void ShouldRoundTripUpdateFillFactor()
    {
        var original = new Index { Name = "IX_Test", IndexColumns = "Col1", UpdateFillFactor = true };
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<Index>(json);
        Assert.That(deserialized!.UpdateFillFactor, Is.True);
    }

    [Test]
    public void DefaultValues_AreCorrect()
    {
        var index = new Index();
        Assert.Multiple(() =>
        {
            Assert.That(index.Name, Is.Null);
            Assert.That(index.CompressionType, Is.EqualTo("NONE"));
            Assert.That(index.PrimaryKey, Is.False);
            Assert.That(index.Unique, Is.False);
            Assert.That(index.UniqueConstraint, Is.False);
            Assert.That(index.Clustered, Is.False);
            Assert.That(index.ColumnStore, Is.False);
            Assert.That(index.FillFactor, Is.EqualTo((byte)0));
            Assert.That(index.IndexColumns, Is.Null);
            Assert.That(index.IncludeColumns, Is.Null);
            Assert.That(index.FilterExpression, Is.Null);
            Assert.That(index.UpdateFillFactor, Is.False);
        });
    }

    [Test]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new Index
        {
            Name = "IX_Orders_Date",
            CompressionType = "ROW",
            PrimaryKey = false,
            Unique = true,
            UniqueConstraint = false,
            Clustered = false,
            ColumnStore = false,
            FillFactor = 80,
            IndexColumns = "OrderDate",
            IncludeColumns = "Status, Amount",
            FilterExpression = "[Status] = 'Active'",
            UpdateFillFactor = true
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<Index>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("IX_Orders_Date"));
            Assert.That(deserialized.CompressionType, Is.EqualTo("ROW"));
            Assert.That(deserialized.Unique, Is.True);
            Assert.That(deserialized.FillFactor, Is.EqualTo((byte)80));
            Assert.That(deserialized.IndexColumns, Is.EqualTo("OrderDate"));
            Assert.That(deserialized.IncludeColumns, Is.EqualTo("Status, Amount"));
            Assert.That(deserialized.FilterExpression, Is.EqualTo("[Status] = 'Active'"));
            Assert.That(deserialized.UpdateFillFactor, Is.True);
        });
    }
}

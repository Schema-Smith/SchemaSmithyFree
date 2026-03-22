using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests;

public class XmlIndexTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        var xi = new XmlIndex();
        Assert.Multiple(() =>
        {
            Assert.That(xi.Name, Is.Null);
            Assert.That(xi.IsPrimary, Is.False);
            Assert.That(xi.Column, Is.Null);
            Assert.That(xi.PrimaryIndex, Is.Null);
            Assert.That(xi.SecondaryIndexType, Is.Null);
        });
    }

    [Test]
    public void JsonRoundTrip_PrimaryXmlIndex()
    {
        var original = new XmlIndex
        {
            Name = "PXML_Data",
            IsPrimary = true,
            Column = "XmlData"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<XmlIndex>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("PXML_Data"));
            Assert.That(deserialized.IsPrimary, Is.True);
            Assert.That(deserialized.Column, Is.EqualTo("XmlData"));
            Assert.That(deserialized.PrimaryIndex, Is.Null);
            Assert.That(deserialized.SecondaryIndexType, Is.Null);
        });
    }

    [Test]
    public void JsonRoundTrip_SecondaryXmlIndex()
    {
        var original = new XmlIndex
        {
            Name = "IXML_Data_Path",
            IsPrimary = false,
            Column = "XmlData",
            PrimaryIndex = "PXML_Data",
            SecondaryIndexType = "PATH"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<XmlIndex>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("IXML_Data_Path"));
            Assert.That(deserialized.IsPrimary, Is.False);
            Assert.That(deserialized.Column, Is.EqualTo("XmlData"));
            Assert.That(deserialized.PrimaryIndex, Is.EqualTo("PXML_Data"));
            Assert.That(deserialized.SecondaryIndexType, Is.EqualTo("PATH"));
        });
    }

    [Test]
    [TestCase("VALUE")]
    [TestCase("PATH")]
    [TestCase("PROPERTY")]
    public void JsonRoundTrip_AllSecondaryIndexTypes(string indexType)
    {
        var original = new XmlIndex
        {
            Name = $"IXML_{indexType}",
            Column = "XmlData",
            PrimaryIndex = "PXML_Data",
            SecondaryIndexType = indexType
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<XmlIndex>(json);

        Assert.That(deserialized!.SecondaryIndexType, Is.EqualTo(indexType));
    }
}

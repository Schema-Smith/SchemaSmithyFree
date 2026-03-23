// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests;

public class FullTextIndexTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        var fti = new FullTextIndex();
        Assert.Multiple(() =>
        {
            Assert.That(fti.FullTextCatalog, Is.Null);
            Assert.That(fti.KeyIndex, Is.Null);
            Assert.That(fti.ChangeTracking, Is.Null);
            Assert.That(fti.StopList, Is.Null);
            Assert.That(fti.Columns, Is.Null);
        });
    }

    [Test]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new FullTextIndex
        {
            FullTextCatalog = "FT_Catalog",
            KeyIndex = "PK_MyTable",
            ChangeTracking = "AUTO",
            StopList = "SL_Custom",
            Columns = "Description, Notes"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<FullTextIndex>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.FullTextCatalog, Is.EqualTo("FT_Catalog"));
            Assert.That(deserialized.KeyIndex, Is.EqualTo("PK_MyTable"));
            Assert.That(deserialized.ChangeTracking, Is.EqualTo("AUTO"));
            Assert.That(deserialized.StopList, Is.EqualTo("SL_Custom"));
            Assert.That(deserialized.Columns, Is.EqualTo("Description, Notes"));
        });
    }

    [Test]
    public void Deserialization_WithMissingOptionalFields_UsesDefaults()
    {
        var json = """{"FullTextCatalog": "FT_Cat", "KeyIndex": "PK_Test", "Columns": "Col1"}""";
        var fti = JsonConvert.DeserializeObject<FullTextIndex>(json);

        Assert.Multiple(() =>
        {
            Assert.That(fti!.ChangeTracking, Is.Null);
            Assert.That(fti.StopList, Is.Null);
        });
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests;

public class ForeignKeyTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        var fk = new ForeignKey();
        Assert.Multiple(() =>
        {
            Assert.That(fk.Name, Is.Null);
            Assert.That(fk.Columns, Is.Null);
            Assert.That(fk.RelatedTableSchema, Is.EqualTo("dbo"));
            Assert.That(fk.RelatedTable, Is.Null);
            Assert.That(fk.RelatedColumns, Is.Null);
            Assert.That(fk.DeleteAction, Is.Null);
            Assert.That(fk.UpdateAction, Is.Null);
        });
    }

    [Test]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new ForeignKey
        {
            Name = "FK_Order_Customer",
            Columns = "CustomerId",
            RelatedTableSchema = "sales",
            RelatedTable = "Customer",
            RelatedColumns = "Id",
            DeleteAction = "CASCADE",
            UpdateAction = "NO ACTION"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<ForeignKey>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("FK_Order_Customer"));
            Assert.That(deserialized.Columns, Is.EqualTo("CustomerId"));
            Assert.That(deserialized.RelatedTableSchema, Is.EqualTo("sales"));
            Assert.That(deserialized.RelatedTable, Is.EqualTo("Customer"));
            Assert.That(deserialized.RelatedColumns, Is.EqualTo("Id"));
            Assert.That(deserialized.DeleteAction, Is.EqualTo("CASCADE"));
            Assert.That(deserialized.UpdateAction, Is.EqualTo("NO ACTION"));
        });
    }

    [Test]
    public void Deserialization_WithMissingOptionalFields_UsesDefaults()
    {
        var json = """{"Name": "FK_Test", "Columns": "Col1", "RelatedTable": "Other", "RelatedColumns": "Id"}""";
        var fk = JsonConvert.DeserializeObject<ForeignKey>(json);

        Assert.Multiple(() =>
        {
            Assert.That(fk!.RelatedTableSchema, Is.EqualTo("dbo"));
            Assert.That(fk.DeleteAction, Is.Null);
            Assert.That(fk.UpdateAction, Is.Null);
        });
    }
}

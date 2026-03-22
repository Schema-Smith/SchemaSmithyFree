using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests;

public class ColumnTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        var column = new Column();
        Assert.Multiple(() =>
        {
            Assert.That(column.Name, Is.Null);
            Assert.That(column.DataType, Is.Null);
            Assert.That(column.Nullable, Is.False);
            Assert.That(column.Default, Is.Null);
            Assert.That(column.CheckExpression, Is.Null);
            Assert.That(column.ComputedExpression, Is.Null);
            Assert.That(column.Persisted, Is.False);
            Assert.That(column.Sparse, Is.False);
            Assert.That(column.Collation, Is.Null);
            Assert.That(column.DataMaskFunction, Is.Null);
            Assert.That(column.OldName, Is.EqualTo(""));
        });
    }

    [Test]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new Column
        {
            Name = "Amount",
            DataType = "DECIMAL(18,2)",
            Nullable = true,
            Default = "0.00",
            CheckExpression = "[Amount] >= 0",
            ComputedExpression = null,
            Persisted = false,
            Sparse = true,
            Collation = "Latin1_General_CI_AS",
            DataMaskFunction = "partial(0,\"XXX\",0)",
            OldName = "OldAmount"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<Column>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("Amount"));
            Assert.That(deserialized.DataType, Is.EqualTo("DECIMAL(18,2)"));
            Assert.That(deserialized.Nullable, Is.True);
            Assert.That(deserialized.Default, Is.EqualTo("0.00"));
            Assert.That(deserialized.CheckExpression, Is.EqualTo("[Amount] >= 0"));
            Assert.That(deserialized.Sparse, Is.True);
            Assert.That(deserialized.Collation, Is.EqualTo("Latin1_General_CI_AS"));
            Assert.That(deserialized.DataMaskFunction, Is.EqualTo("partial(0,\"XXX\",0)"));
            Assert.That(deserialized.OldName, Is.EqualTo("OldAmount"));
        });
    }

    [Test]
    public void Deserialization_WithMissingOptionalFields_UsesDefaults()
    {
        var json = """{"Name": "Id", "DataType": "INT"}""";
        var column = JsonConvert.DeserializeObject<Column>(json);

        Assert.Multiple(() =>
        {
            Assert.That(column!.Name, Is.EqualTo("Id"));
            Assert.That(column.DataType, Is.EqualTo("INT"));
            Assert.That(column.Nullable, Is.False);
            Assert.That(column.Sparse, Is.False);
            Assert.That(column.Persisted, Is.False);
            Assert.That(column.OldName, Is.EqualTo(""));
        });
    }

    [Test]
    public void JsonRoundTrip_ComputedColumn_PreservesExpressionAndPersisted()
    {
        var original = new Column
        {
            Name = "FullName",
            DataType = "NVARCHAR(200)",
            ComputedExpression = "[FirstName] + ' ' + [LastName]",
            Persisted = true
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<Column>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.ComputedExpression, Is.EqualTo("[FirstName] + ' ' + [LastName]"));
            Assert.That(deserialized.Persisted, Is.True);
        });
    }
}

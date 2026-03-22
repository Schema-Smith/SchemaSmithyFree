using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests;

public class StatisticTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        var stat = new Statistic();
        Assert.Multiple(() =>
        {
            Assert.That(stat.Name, Is.Null);
            Assert.That(stat.Columns, Is.Null);
            Assert.That(stat.SampleSize, Is.EqualTo((byte)0));
            Assert.That(stat.FilterExpression, Is.Null);
        });
    }

    [Test]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new Statistic
        {
            Name = "ST_DateRange",
            Columns = "DateCreated, Status",
            SampleSize = 75,
            FilterExpression = "[Status] = 'Active'"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<Statistic>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("ST_DateRange"));
            Assert.That(deserialized.Columns, Is.EqualTo("DateCreated, Status"));
            Assert.That(deserialized.SampleSize, Is.EqualTo((byte)75));
            Assert.That(deserialized.FilterExpression, Is.EqualTo("[Status] = 'Active'"));
        });
    }

    [Test]
    public void Deserialization_WithMissingOptionalFields_UsesDefaults()
    {
        var json = """{"Name": "ST_Test", "Columns": "Col1"}""";
        var stat = JsonConvert.DeserializeObject<Statistic>(json);

        Assert.Multiple(() =>
        {
            Assert.That(stat!.SampleSize, Is.EqualTo((byte)0));
            Assert.That(stat.FilterExpression, Is.Null);
        });
    }
}

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
}

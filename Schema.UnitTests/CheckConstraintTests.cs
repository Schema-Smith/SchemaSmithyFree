using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests;

public class CheckConstraintTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        var cc = new CheckConstraint();
        Assert.Multiple(() =>
        {
            Assert.That(cc.Name, Is.Null);
            Assert.That(cc.Expression, Is.Null);
        });
    }

    [Test]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new CheckConstraint
        {
            Name = "CK_Status",
            Expression = "[Status] IN ('Active','Inactive')"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<CheckConstraint>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Name, Is.EqualTo("CK_Status"));
            Assert.That(deserialized.Expression, Is.EqualTo("[Status] IN ('Active','Inactive')"));
        });
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class CheckConstraintTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var cc = new CheckConstraint();

            Assert.That(cc.Name, Is.EqualTo(""));
            Assert.That(cc.Expression, Is.EqualTo(""));
            Assert.That(cc.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var cc = new CheckConstraint
            {
                Name = "CK_Age",
                Expression = "Age >= 0",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(cc);
            var deserialized = JsonConvert.DeserializeObject<CheckConstraint>(json);

            Assert.That(deserialized.Name, Is.EqualTo("CK_Age"));
            Assert.That(deserialized.Expression, Is.EqualTo("Age >= 0"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

        [Test]
        public void JsonRoundTrip_PreservesVariantName()
        {
            var cc = new CheckConstraint { Name = "CK_Test", Expression = "Col1 > 0", ShouldApplyExpression = "1=1", VariantName = "EU region" };
            var json = JsonConvert.SerializeObject(cc);
            var deserialized = JsonConvert.DeserializeObject<CheckConstraint>(json);
            Assert.That(deserialized.VariantName, Is.EqualTo("EU region"));
        }

    }
}

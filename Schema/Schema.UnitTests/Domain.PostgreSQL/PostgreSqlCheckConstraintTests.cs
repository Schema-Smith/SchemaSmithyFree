// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.PostgreSQL;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlCheckConstraintTests
    {
        [Test]
        public void InheritsFromCheckConstraint()
        {
            Assert.That(new PostgreSqlCheckConstraint(), Is.InstanceOf<CheckConstraint>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var cc = new PostgreSqlCheckConstraint();

            Assert.That(cc.Deferrable, Is.False);
            Assert.That(cc.InitiallyDeferred, Is.False);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var cc = new PostgreSqlCheckConstraint
            {
                Name = "ck_positive_amount",
                Expression = "amount > 0",
                Deferrable = true,
                InitiallyDeferred = true,
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(cc);
            var deserialized = JsonConvert.DeserializeObject<PostgreSqlCheckConstraint>(json);

            Assert.That(deserialized.Name, Is.EqualTo("ck_positive_amount"));
            Assert.That(deserialized.Expression, Is.EqualTo("amount > 0"));
            Assert.That(deserialized.Deferrable, Is.True);
            Assert.That(deserialized.InitiallyDeferred, Is.True);
        }
    }
}

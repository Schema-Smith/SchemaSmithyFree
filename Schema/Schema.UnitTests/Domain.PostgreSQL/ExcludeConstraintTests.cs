// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.PostgreSQL;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class ExcludeConstraintTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromDynamicBase()
        {
            Assert.That(new ExcludeConstraint(), Is.InstanceOf<DynamicBase>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var ec = new ExcludeConstraint();

            Assert.That(ec.Name, Is.Null);
            Assert.That(ec.AccessMethod, Is.Null);
            Assert.That(ec.ExcludeColumns, Is.Not.Null.And.Empty);
            Assert.That(ec.FilterExpression, Is.Null);
            Assert.That(ec.ShouldApplyExpression, Is.Null);
            Assert.That(ec.Deferrable, Is.False);
            Assert.That(ec.InitiallyDeferred, Is.False);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var ec = new ExcludeConstraint
            {
                Name = "ex_no_overlap",
                AccessMethod = "gist",
                ExcludeColumns =
                [
                    new ExcludeConstraint.ExcludeColumn { Column = "period", Operator = "&&" },
                    new ExcludeConstraint.ExcludeColumn { Column = "room_id", Operator = "=" }
                ],
                FilterExpression = "is_active = true",
                ShouldApplyExpression = "SELECT 1",
                Deferrable = true,
                InitiallyDeferred = true
            };

            var json = JsonConvert.SerializeObject(ec);
            var deserialized = JsonConvert.DeserializeObject<ExcludeConstraint>(json);

            Assert.That(deserialized.Name, Is.EqualTo("ex_no_overlap"));
            Assert.That(deserialized.AccessMethod, Is.EqualTo("gist"));
            Assert.That(deserialized.ExcludeColumns, Has.Count.EqualTo(2));
            Assert.That(deserialized.ExcludeColumns[0].Column, Is.EqualTo("period"));
            Assert.That(deserialized.ExcludeColumns[0].Operator, Is.EqualTo("&&"));
            Assert.That(deserialized.ExcludeColumns[1].Column, Is.EqualTo("room_id"));
            Assert.That(deserialized.ExcludeColumns[1].Operator, Is.EqualTo("="));
            Assert.That(deserialized.FilterExpression, Is.EqualTo("is_active = true"));
            Assert.That(deserialized.Deferrable, Is.True);
            Assert.That(deserialized.InitiallyDeferred, Is.True);
        }

        [Test]
        public void JsonRoundTrip_PreservesVariantName()
        {
            var obj = new ExcludeConstraint
            {
                Name = "ex_no_overlap",
                ExcludeColumns = [new ExcludeConstraint.ExcludeColumn { Column = "period", Operator = "&&" }],
                ShouldApplyExpression = "1=1",
                VariantName = "EU region"
            };
            var json = JsonConvert.SerializeObject(obj);
            var deserialized = JsonConvert.DeserializeObject<ExcludeConstraint>(json);
            Assert.That(deserialized.VariantName, Is.EqualTo("EU region"));
        }

        [Test]
        public void ExcludeColumn_DefaultValues()
        {
            var col = new ExcludeConstraint.ExcludeColumn();

            Assert.That(col.Column, Is.Null);
            Assert.That(col.Operator, Is.Null);
        }

        [Test]
        public void ExcludeColumn_JsonRoundTrip()
        {
            var col = new ExcludeConstraint.ExcludeColumn { Column = "tsrange_col", Operator = "&&" };

            var json = JsonConvert.SerializeObject(col);
            var deserialized = JsonConvert.DeserializeObject<ExcludeConstraint.ExcludeColumn>(json);

            Assert.That(deserialized.Column, Is.EqualTo("tsrange_col"));
            Assert.That(deserialized.Operator, Is.EqualTo("&&"));
        }
    }
}

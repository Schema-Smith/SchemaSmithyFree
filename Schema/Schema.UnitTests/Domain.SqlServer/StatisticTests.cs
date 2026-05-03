// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class StatisticTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromDynamicBase()
        {
            Assert.That(new Statistic(), Is.InstanceOf<DynamicBase>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var stat = new Statistic();

            Assert.That(stat.Name, Is.Null);
            Assert.That(stat.Columns, Is.Null);
            Assert.That(stat.SampleSize, Is.EqualTo((byte)0));
            Assert.That(stat.FilterExpression, Is.Null);
            Assert.That(stat.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var stat = new Statistic
            {
                Name = "ST_Customer_Name",
                Columns = "LastName, FirstName",
                SampleSize = 75,
                FilterExpression = "[IsActive] = 1",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(stat);
            var deserialized = JsonConvert.DeserializeObject<Statistic>(json);

            Assert.That(deserialized.Name, Is.EqualTo("ST_Customer_Name"));
            Assert.That(deserialized.Columns, Is.EqualTo("LastName, FirstName"));
            Assert.That(deserialized.SampleSize, Is.EqualTo((byte)75));
            Assert.That(deserialized.FilterExpression, Is.EqualTo("[IsActive] = 1"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

        [Test]
        public void JsonSerialization_OmitsNullProperties()
        {
            var stat = new Statistic { Name = "ST_Test", Columns = "Col1" };
            var json = JsonConvert.SerializeObject(stat, NullIgnore);

            Assert.That(json, Does.Not.Contain("FilterExpression"));
            Assert.That(json, Does.Not.Contain("ShouldApplyExpression"));
        }
    }
}

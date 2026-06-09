// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.PostgreSQL;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlStatisticTests
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
            Assert.That(stat.Kind, Is.Null);
            Assert.That(stat.StatisticsColumns, Is.Null);
            Assert.That(stat.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var stat = new Statistic
            {
                Name = "st_customer_name",
                Kind = "dependencies",
                StatisticsColumns = "last_name, first_name",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(stat);
            var deserialized = JsonConvert.DeserializeObject<Statistic>(json);

            Assert.That(deserialized.Name, Is.EqualTo("st_customer_name"));
            Assert.That(deserialized.Kind, Is.EqualTo("dependencies"));
            Assert.That(deserialized.StatisticsColumns, Is.EqualTo("last_name, first_name"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

        [Test]
        public void JsonRoundTrip_PreservesVariantName()
        {
            var obj = new Statistic { Name = "ST_Test", StatisticsColumns = "col1, col2", ShouldApplyExpression = "1=1", VariantName = "EU region" };
            var json = JsonConvert.SerializeObject(obj);
            var deserialized = JsonConvert.DeserializeObject<Statistic>(json);
            Assert.That(deserialized.VariantName, Is.EqualTo("EU region"));
        }

        [Test]
        public void DifferentFromSqlServerStatistic()
        {
            // PostgreSQL Statistic has Kind and StatisticsColumns
            // SQL Server Statistic has Columns, SampleSize, FilterExpression
            var pgStat = new Statistic();
            var sqlStat = new Schema.Domain.SqlServer.Statistic();

            Assert.That(pgStat.GetType().Namespace, Is.EqualTo("Schema.Domain.PostgreSQL"));
            Assert.That(sqlStat.GetType().Namespace, Is.EqualTo("Schema.Domain.SqlServer"));
        }
    }
}

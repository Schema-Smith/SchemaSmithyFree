// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Domain.PostgreSQL;
using Schema.Utility;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlMaterializedViewTests
    {
        [Test]
        public void InheritsFromDynamicBase()
        {
            var view = new PostgreSqlMaterializedView();
            Assert.That(view, Is.InstanceOf<DynamicBase>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var view = new PostgreSqlMaterializedView();
            Assert.Multiple(() =>
            {
                // Slice 1 (Schema Templates): bare-constructor Schema is null;
                // SchemaDefaultResolver fills it. See SchemaDefaultResolverTests.
                Assert.That(view.Schema, Is.Null);
                Assert.That(view.Name, Is.EqualTo(""));
                Assert.That(view.Definition, Is.EqualTo(""));
                Assert.That(view.WithData, Is.True);
                Assert.That(view.Tablespace, Is.Null);
                Assert.That(view.AccessMethod, Is.Null);
                Assert.That(view.ShouldApplyExpression, Is.Null);
                Assert.That(view.Indexes, Is.Not.Null);
                Assert.That(view.Indexes, Is.Empty);
            });
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var view = new PostgreSqlMaterializedView
            {
                Schema = "sales",
                Name = "mv_order_summary",
                Definition = "SELECT customer_id, SUM(total) AS total\nFROM orders\nGROUP BY customer_id",
                WithData = false,
                Tablespace = "fast_storage",
                AccessMethod = "heap",
                ShouldApplyExpression = "{{IsProduction}}",
                Indexes =
                [
                    new PostgreSqlIndex
                    {
                        Name = "ix_mv_customer",
                        Unique = true,
                        IndexColumns = "customer_id",
                        AccessMethod = "btree",
                        FillFactor = 90
                    }
                ]
            };

            var json = JsonHelper.Serialize(view);
            var deserialized = JsonConvert.DeserializeObject<PostgreSqlMaterializedView>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Schema, Is.EqualTo("sales"));
                Assert.That(deserialized.Name, Is.EqualTo("mv_order_summary"));
                Assert.That(deserialized.Definition, Does.Contain("SELECT customer_id"));
                Assert.That(deserialized.WithData, Is.False);
                Assert.That(deserialized.Tablespace, Is.EqualTo("fast_storage"));
                Assert.That(deserialized.AccessMethod, Is.EqualTo("heap"));
                Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("{{IsProduction}}"));
                Assert.That(deserialized.Indexes, Has.Count.EqualTo(1));
                Assert.That(deserialized.Indexes[0], Is.InstanceOf<PostgreSqlIndex>());
                Assert.That(deserialized.Indexes[0].Name, Is.EqualTo("ix_mv_customer"));
                Assert.That(deserialized.Indexes[0].FillFactor, Is.EqualTo(90));
            });
        }

        [Test]
        public void CustomProperties_SurviveRoundTrip()
        {
            var view = new PostgreSqlMaterializedView { Name = "test" };
            view.Extensions = new JObject { ["RefreshPolicy"] = "OnDeploy" };

            var json = JsonHelper.Serialize(view);
            var deserialized = JsonConvert.DeserializeObject<PostgreSqlMaterializedView>(json);

            Assert.That(deserialized.Extensions?["RefreshPolicy"]?.ToString(), Is.EqualTo("OnDeploy"));
        }

        [Test]
        public void DefaultValueHandling_OmitsDefaults()
        {
            var view = new PostgreSqlMaterializedView { Name = "test", Definition = "SELECT 1" };
            var json = JsonHelper.Serialize(view);

            // WithData=true is default, should be omitted
            Assert.That(json, Does.Not.Contain("\"WithData\""));
            // Schema="public" is default, should be omitted
            Assert.That(json, Does.Not.Contain("\"Schema\""));
        }
    }
}

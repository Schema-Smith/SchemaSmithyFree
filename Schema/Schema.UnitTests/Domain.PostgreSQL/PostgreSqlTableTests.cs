// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.PostgreSQL;

using Schema.Delivery;
namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlTableTests
    {
        [Test]
        public void InheritsFromTable()
        {
            Assert.That(new PostgreSqlTable(), Is.InstanceOf<Table>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var table = new PostgreSqlTable();

            // Slice 1 (Schema Templates): bare-constructor Schema is null; the load-time
            // SchemaDefaultResolver fills it (with "public" for regular templates, "{{SchemaName}}"
            // for schema templates). See PostgreSqlTableDefaultingTests for the resolved-state contract.
            Assert.That(table.Schema, Is.Null);
            Assert.That(table.Statistics, Is.Not.Null.And.Empty);
            Assert.That(table.ExcludeConstraints, Is.Not.Null.And.Empty);
            Assert.That(table.DataDelivery, Is.Empty);
            Assert.That(table.RowLevelSecurity, Is.False);
            Assert.That(table.ForceRowLevelSecurity, Is.False);
            Assert.That(table.AccessMethod, Is.Null);
            Assert.That(table.PersistenceType, Is.Null);
            Assert.That(table.ReplicaIdentity, Is.Null);
            Assert.That(table.ReplicaIdentityIndex, Is.Null);
            Assert.That(table.UpdateFillFactor, Is.False);
            Assert.That(table.FillFactor, Is.EqualTo((short)0));
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var table = new PostgreSqlTable
            {
                Name = "customer",
                Schema = "sales",
                DataDelivery = [new PostgreSqlDataDelivery { MergeDisableRules = true, MergeUpdateDescendents = true }],
                RowLevelSecurity = true,
                ForceRowLevelSecurity = true,
                AccessMethod = "heap",
                PersistenceType = "UNLOGGED",
                ReplicaIdentity = "INDEX",
                ReplicaIdentityIndex = "ix_customer_uq",
                UpdateFillFactor = true,
                FillFactor = 90
            };

            table.Statistics.Add(new Schema.Domain.PostgreSQL.Statistic { Name = "st_name", Kind = "dependencies", StatisticsColumns = "last_name, first_name" });
            table.ExcludeConstraints.Add(new ExcludeConstraint
            {
                Name = "ex_no_overlap",
                AccessMethod = "gist",
                ExcludeColumns = [new ExcludeConstraint.ExcludeColumn { Column = "period", Operator = "&&" }]
            });

            var json = JsonConvert.SerializeObject(table);
            var deserialized = JsonConvert.DeserializeObject<PostgreSqlTable>(json);

            Assert.That(deserialized.Schema, Is.EqualTo("sales"));
            Assert.That(deserialized.DataDelivery, Is.Not.Empty);
            Assert.That(deserialized.DataDelivery[0].MergeDisableRules, Is.True);
            Assert.That(deserialized.DataDelivery[0].MergeUpdateDescendents, Is.True);
            Assert.That(deserialized.RowLevelSecurity, Is.True);
            Assert.That(deserialized.ForceRowLevelSecurity, Is.True);
            Assert.That(deserialized.AccessMethod, Is.EqualTo("heap"));
            Assert.That(deserialized.PersistenceType, Is.EqualTo("UNLOGGED"));
            Assert.That(deserialized.ReplicaIdentity, Is.EqualTo("INDEX"));
            Assert.That(deserialized.ReplicaIdentityIndex, Is.EqualTo("ix_customer_uq"));
            Assert.That(deserialized.FillFactor, Is.EqualTo((short)90));
            Assert.That(deserialized.Statistics, Has.Count.EqualTo(1));
            Assert.That(deserialized.Statistics[0].Name, Is.EqualTo("st_name"));
            Assert.That(deserialized.ExcludeConstraints, Has.Count.EqualTo(1));
            Assert.That(deserialized.ExcludeConstraints[0].Name, Is.EqualTo("ex_no_overlap"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var table = new PostgreSqlTable { Name = "test" };
            var json = JsonConvert.SerializeObject(table);

            Assert.That(json, Does.Not.Contain("CompressionType"));
            Assert.That(json, Does.Not.Contain("IsTemporal"));
            Assert.That(json, Does.Not.Contain("XmlIndexes"));
            Assert.That(json, Does.Not.Contain("EnableCDC"));
            Assert.That(json, Does.Not.Contain("Engine"));
            Assert.That(json, Does.Not.Contain("RowFormat"));
            Assert.That(json, Does.Not.Contain("CharacterSet"));
            Assert.That(json, Does.Not.Contain("AutoIncrementValue"));
        }
    }
}

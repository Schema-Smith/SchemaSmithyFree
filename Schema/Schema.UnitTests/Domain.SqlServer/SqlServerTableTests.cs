// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

using SchemaSmith.Pro;
namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class SqlServerTableTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromTable()
        {
            Assert.That(new SqlServerTable(), Is.InstanceOf<Table>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var table = new SqlServerTable();

            Assert.That(table.Schema, Is.EqualTo("dbo"));
            Assert.That(table.CompressionType, Is.EqualTo("NONE"));
            Assert.That(table.IsTemporal, Is.False);
            Assert.That(table.XmlIndexes, Is.Not.Null.And.Empty);
            Assert.That(table.Statistics, Is.Not.Null.And.Empty);
            Assert.That(table.FullTextIndex, Is.Null);
            Assert.That(table.UpdateFillFactor, Is.False);
            Assert.That(table.EnableCDC, Is.False);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var table = new SqlServerTable
            {
                Name = "Customer",
                Schema = "sales",
                CompressionType = "PAGE",
                IsTemporal = true,
                UpdateFillFactor = true,
                EnableCDC = true,
                FullTextIndex = new FullTextIndex
                {
                    FullTextCatalog = "FTC_Customer",
                    KeyIndex = "PK_Customer",
                    Columns = "Name, Description"
                }
            };

            table.XmlIndexes.Add(new XmlIndex { Name = "IX_XML_Data", Column = "XmlData", IsPrimary = true });
            table.Statistics.Add(new Schema.Domain.SqlServer.Statistic { Name = "ST_Name", Columns = "LastName" });

            var json = JsonConvert.SerializeObject(table);
            var deserialized = JsonConvert.DeserializeObject<SqlServerTable>(json);

            Assert.That(deserialized.Name, Is.EqualTo("Customer"));
            Assert.That(deserialized.Schema, Is.EqualTo("sales"));
            Assert.That(deserialized.CompressionType, Is.EqualTo("PAGE"));
            Assert.That(deserialized.IsTemporal, Is.True);
            Assert.That(deserialized.EnableCDC, Is.True);
            Assert.That(deserialized.FullTextIndex, Is.Not.Null);
            Assert.That(deserialized.FullTextIndex.FullTextCatalog, Is.EqualTo("FTC_Customer"));
            Assert.That(deserialized.XmlIndexes, Has.Count.EqualTo(1));
            Assert.That(deserialized.XmlIndexes[0].Name, Is.EqualTo("IX_XML_Data"));
            Assert.That(deserialized.Statistics, Has.Count.EqualTo(1));
            Assert.That(deserialized.Statistics[0].Name, Is.EqualTo("ST_Name"));
        }

        [Test]
        public void JsonSerialization_OmitsDefaultAndNullProperties()
        {
            var table = new SqlServerTable { Name = "Test" };
            var json = JsonConvert.SerializeObject(table, NullIgnore);

            Assert.That(json, Does.Not.Contain("IsTemporal"));
            Assert.That(json, Does.Not.Contain("UpdateFillFactor"));
            Assert.That(json, Does.Not.Contain("EnableCDC"));
            Assert.That(json, Does.Not.Contain("FullTextIndex"));
        }

        [Test]
        public void BaseTableProperties_AreInherited()
        {
            var table = new SqlServerTable
            {
                Name = "Test",
                DataDelivery = new DataDelivery { MergeType = "Insert/Update", MatchColumns = "Id", MergeDisableTriggers = true }
            };

            Assert.That(table.DataDelivery.MergeType, Is.EqualTo("Insert/Update"));
            Assert.That(table.DataDelivery.MatchColumns, Is.EqualTo("Id"));
            Assert.That(table.DataDelivery.MergeDisableTriggers, Is.True);
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var table = new SqlServerTable { Name = "Test" };
            var json = JsonConvert.SerializeObject(table);

            Assert.That(json, Does.Not.Contain("ExcludeConstraints"));
            Assert.That(json, Does.Not.Contain("RowLevelSecurity"));
            Assert.That(json, Does.Not.Contain("PersistenceType"));
            Assert.That(json, Does.Not.Contain("Engine"));
            Assert.That(json, Does.Not.Contain("RowFormat"));
            Assert.That(json, Does.Not.Contain("CharacterSet"));
            Assert.That(json, Does.Not.Contain("AutoIncrementValue"));
        }
    }
}

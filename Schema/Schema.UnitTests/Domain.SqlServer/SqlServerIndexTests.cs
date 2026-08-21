// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class SqlServerIndexTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromIndex()
        {
            Assert.That(new SqlServerIndex(), Is.InstanceOf<Index>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var index = new SqlServerIndex();

            Assert.That(index.CompressionType, Is.EqualTo("NONE"));
            Assert.That(index.Clustered, Is.False);
            Assert.That(index.ColumnStore, Is.False);
            Assert.That(index.FillFactor, Is.EqualTo((byte)0));
            Assert.That(index.IncludeColumns, Is.Null);
            Assert.That(index.FilterExpression, Is.Null);
            Assert.That(index.UpdateFillFactor, Is.False);
            Assert.That(index.FileGroup, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var index = new SqlServerIndex
            {
                Name = "IX_Customer_Name",
                PrimaryKey = false,
                Unique = true,
                IndexColumns = "LastName, FirstName",
                CompressionType = "PAGE",
                Clustered = false,
                ColumnStore = false,
                FillFactor = 80,
                IncludeColumns = "Email",
                FilterExpression = "[IsActive] = 1",
                UpdateFillFactor = true,
                FileGroup = "[FG_Indexes]",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(index);
            var deserialized = JsonConvert.DeserializeObject<SqlServerIndex>(json);

            Assert.That(deserialized.Name, Is.EqualTo("IX_Customer_Name"));
            Assert.That(deserialized.Unique, Is.True);
            Assert.That(deserialized.IndexColumns, Is.EqualTo("LastName, FirstName"));
            Assert.That(deserialized.CompressionType, Is.EqualTo("PAGE"));
            Assert.That(deserialized.FillFactor, Is.EqualTo((byte)80));
            Assert.That(deserialized.IncludeColumns, Is.EqualTo("Email"));
            Assert.That(deserialized.FilterExpression, Is.EqualTo("[IsActive] = 1"));
            Assert.That(deserialized.UpdateFillFactor, Is.True);
            Assert.That(deserialized.FileGroup, Is.EqualTo("[FG_Indexes]"));
        }

        [Test]
        public void JsonSerialization_OmitsDefaultAndNullProperties()
        {
            var index = new SqlServerIndex { Name = "IX_Test", IndexColumns = "Col1" };
            var json = JsonConvert.SerializeObject(index, NullIgnore);

            Assert.That(json, Does.Not.Contain("IncludeColumns"));
            Assert.That(json, Does.Not.Contain("FilterExpression"));
            Assert.That(json, Does.Not.Contain("Clustered"));
            Assert.That(json, Does.Not.Contain("ColumnStore"));
            Assert.That(json, Does.Not.Contain("UpdateFillFactor"));
            Assert.That(json, Does.Not.Contain("FileGroup"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var index = new SqlServerIndex { Name = "IX_Test", IndexColumns = "Col1" };
            var json = JsonConvert.SerializeObject(index);

            Assert.That(json, Does.Not.Contain("AccessMethod"));
            Assert.That(json, Does.Not.Contain("NullsNotDistinct"));
            Assert.That(json, Does.Not.Contain("Deferrable"));
            Assert.That(json, Does.Not.Contain("Visible"));
            Assert.That(json, Does.Not.Contain("IndexType"));
        }
    }
}

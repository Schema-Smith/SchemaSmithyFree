// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.PostgreSQL;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlIndexTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromIndex()
        {
            Assert.That(new PostgreSqlIndex(), Is.InstanceOf<Index>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var index = new PostgreSqlIndex();

            Assert.That(index.Clustered, Is.False);
            Assert.That(index.IncludeColumns, Is.Null);
            Assert.That(index.AccessMethod, Is.Null);
            Assert.That(index.FilterExpression, Is.Null);
            Assert.That(index.FillFactor, Is.EqualTo(0));
            Assert.That(index.NullsNotDistinct, Is.False);
            Assert.That(index.Deferrable, Is.False);
            Assert.That(index.InitiallyDeferred, Is.False);
            Assert.That(index.UpdateFillFactor, Is.False);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var index = new PostgreSqlIndex
            {
                Name = "ix_customer_email",
                Unique = true,
                IndexColumns = "email",
                Clustered = false,
                IncludeColumns = "name",
                AccessMethod = "btree",
                FilterExpression = "is_active = true",
                FillFactor = 90,
                NullsNotDistinct = true,
                Deferrable = true,
                InitiallyDeferred = true,
                UpdateFillFactor = true
            };

            var json = JsonConvert.SerializeObject(index);
            var deserialized = JsonConvert.DeserializeObject<PostgreSqlIndex>(json);

            Assert.That(deserialized.IncludeColumns, Is.EqualTo("name"));
            Assert.That(deserialized.AccessMethod, Is.EqualTo("btree"));
            Assert.That(deserialized.FilterExpression, Is.EqualTo("is_active = true"));
            Assert.That(deserialized.FillFactor, Is.EqualTo(90));
            Assert.That(deserialized.NullsNotDistinct, Is.True);
            Assert.That(deserialized.Deferrable, Is.True);
            Assert.That(deserialized.InitiallyDeferred, Is.True);
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var index = new PostgreSqlIndex { Name = "ix_test", IndexColumns = "col1" };
            var json = JsonConvert.SerializeObject(index);

            Assert.That(json, Does.Not.Contain("CompressionType"));
            Assert.That(json, Does.Not.Contain("ColumnStore"));
            Assert.That(json, Does.Not.Contain("Visible"));
            Assert.That(json, Does.Not.Contain("IndexType"));
            Assert.That(json, Does.Not.Contain("Comment"));
        }
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.MySQL;

namespace Schema.UnitTests.Domain.MySQL
{
    [TestFixture]
    public class MySqlIndexTests
    {
        [Test]
        public void InheritsFromIndex()
        {
            Assert.That(new MySqlIndex(), Is.InstanceOf<Index>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var index = new MySqlIndex();

            Assert.That(index.IndexType, Is.EqualTo("BTREE"));
            Assert.That(index.Visible, Is.True);
            Assert.That(index.Comment, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var index = new MySqlIndex
            {
                Name = "ix_customer_email",
                Unique = true,
                IndexColumns = "email",
                IndexType = "HASH",
                Visible = false,
                Comment = "Email lookup index"
            };

            var json = JsonConvert.SerializeObject(index);
            var deserialized = JsonConvert.DeserializeObject<MySqlIndex>(json);

            Assert.That(deserialized.IndexType, Is.EqualTo("HASH"));
            Assert.That(deserialized.Visible, Is.False);
            Assert.That(deserialized.Comment, Is.EqualTo("Email lookup index"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var index = new MySqlIndex { Name = "ix_test", IndexColumns = "col1" };
            var json = JsonConvert.SerializeObject(index);

            Assert.That(json, Does.Not.Contain("CompressionType"));
            Assert.That(json, Does.Not.Contain("Clustered"));
            Assert.That(json, Does.Not.Contain("ColumnStore"));
            Assert.That(json, Does.Not.Contain("FillFactor"));
            Assert.That(json, Does.Not.Contain("IncludeColumns"));
            Assert.That(json, Does.Not.Contain("FilterExpression"));
            Assert.That(json, Does.Not.Contain("AccessMethod"));
            Assert.That(json, Does.Not.Contain("NullsNotDistinct"));
            Assert.That(json, Does.Not.Contain("Deferrable"));
        }
    }
}

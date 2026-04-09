// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class IndexTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var index = new Index();

            Assert.That(index.Name, Is.EqualTo(""));
            Assert.That(index.PrimaryKey, Is.False);
            Assert.That(index.Unique, Is.False);
            Assert.That(index.UniqueConstraint, Is.False);
            Assert.That(index.IndexColumns, Is.EqualTo(""));
            Assert.That(index.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var index = new Index
            {
                Name = "PK_Table",
                PrimaryKey = true,
                Unique = true,
                UniqueConstraint = false,
                IndexColumns = "Id ASC",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(index);
            var deserialized = JsonConvert.DeserializeObject<Index>(json);

            Assert.That(deserialized.Name, Is.EqualTo("PK_Table"));
            Assert.That(deserialized.PrimaryKey, Is.True);
            Assert.That(deserialized.Unique, Is.True);
            Assert.That(deserialized.UniqueConstraint, Is.False);
            Assert.That(deserialized.IndexColumns, Is.EqualTo("Id ASC"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

    }
}

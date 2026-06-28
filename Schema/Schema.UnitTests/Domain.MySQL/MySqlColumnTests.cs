// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.UnitTests.Domain.MySQL
{
    [TestFixture]
    public class MySqlColumnTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromColumn()
        {
            Assert.That(new MySqlColumn(), Is.InstanceOf<Column>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var column = new MySqlColumn();

            Assert.That(column.AutoIncrement, Is.False);
            Assert.That(column.Generated, Is.Null);
            Assert.That(column.GenerationExpression, Is.Null);
            Assert.That(column.CharacterSet, Is.Null);
            Assert.That(column.Collation, Is.Null);
            Assert.That(column.Comment, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var column = new MySqlColumn
            {
                Name = "description",
                DataType = "varchar(255)",
                Nullable = true,
                AutoIncrement = false,
                Generated = "STORED",
                GenerationExpression = "CONCAT(first_name, ' ', last_name)",
                CharacterSet = "utf8mb4",
                Collation = "utf8mb4_unicode_ci",
                Comment = "Full name computed column",
                ShouldApplyExpression = "SELECT 1",
                OldName = "desc"
            };

            var json = JsonConvert.SerializeObject(column);
            var deserialized = JsonConvert.DeserializeObject<MySqlColumn>(json);

            Assert.That(deserialized.Generated, Is.EqualTo("STORED"));
            Assert.That(deserialized.GenerationExpression, Is.EqualTo("CONCAT(first_name, ' ', last_name)"));
            Assert.That(deserialized.CharacterSet, Is.EqualTo("utf8mb4"));
            Assert.That(deserialized.Collation, Is.EqualTo("utf8mb4_unicode_ci"));
            Assert.That(deserialized.Comment, Is.EqualTo("Full name computed column"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var column = new MySqlColumn { Name = "Id", DataType = "int" };
            var json = JsonConvert.SerializeObject(column);

            Assert.That(json, Does.Not.Contain("ComputedExpression"));
            Assert.That(json, Does.Not.Contain("Sparse"));
            Assert.That(json, Does.Not.Contain("Persisted"));
            Assert.That(json, Does.Not.Contain("DataMaskFunction"));
            Assert.That(json, Does.Not.Contain("EncryptionType"));
            Assert.That(json, Does.Not.Contain("Storage"));
            Assert.That(json, Does.Not.Contain("Compression"));
            Assert.That(json, Does.Not.Contain("Virtual"));
        }

        [Test]
        public void MySqlColumn_PreservesCheckExpression_ThroughDeserializeAndSerializeAll()
        {
            const string json = """
            {"Name":"T","Schema":"public","Columns":[
                {"Name":"Quantity","DataType":"int","CheckExpression":"Quantity > 0"}
            ]}
            """;
            var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL);
            var roundTripped = JsonHelper.SerializeAll(table);
            Assert.That(roundTripped, Does.Contain("CheckExpression"));
            Assert.That(roundTripped, Does.Contain("Quantity > 0"));
        }
    }
}

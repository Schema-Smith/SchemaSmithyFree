// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.PostgreSQL;
using Schema.Utility;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlColumnTests
    {
        [Test]
        public void InheritsFromColumn()
        {
            Assert.That(new PostgreSqlColumn(), Is.InstanceOf<Column>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var column = new PostgreSqlColumn();

            Assert.That(column.Collation, Is.Null);
            Assert.That(column.Generated, Is.Null);
            Assert.That(column.GenerationExpression, Is.Null);
            Assert.That(column.Virtual, Is.False);
            Assert.That(column.Storage, Is.Null);
            Assert.That(column.Compression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var column = new PostgreSqlColumn
            {
                Name = "description",
                DataType = "text",
                Nullable = true,
                Collation = "en_US.utf8",
                Generated = "ALWAYS",
                GenerationExpression = "first_name || ' ' || last_name",
                Virtual = false,
                Storage = "EXTENDED",
                Compression = "lz4",
                ShouldApplyExpression = "SELECT 1",
                OldName = "desc"
            };

            var json = JsonConvert.SerializeObject(column);
            var deserialized = JsonConvert.DeserializeObject<PostgreSqlColumn>(json);

            Assert.That(deserialized.Name, Is.EqualTo("description"));
            Assert.That(deserialized.DataType, Is.EqualTo("text"));
            Assert.That(deserialized.Collation, Is.EqualTo("en_US.utf8"));
            Assert.That(deserialized.Generated, Is.EqualTo("ALWAYS"));
            Assert.That(deserialized.GenerationExpression, Is.EqualTo("first_name || ' ' || last_name"));
            Assert.That(deserialized.Storage, Is.EqualTo("EXTENDED"));
            Assert.That(deserialized.Compression, Is.EqualTo("lz4"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var column = new PostgreSqlColumn { Name = "Id", DataType = "int" };
            var json = JsonConvert.SerializeObject(column);

            Assert.That(json, Does.Not.Contain("ComputedExpression"));
            Assert.That(json, Does.Not.Contain("Sparse"));
            Assert.That(json, Does.Not.Contain("DataMaskFunction"));
            Assert.That(json, Does.Not.Contain("EncryptionType"));
            Assert.That(json, Does.Not.Contain("AutoIncrement"));
            Assert.That(json, Does.Not.Contain("CharacterSet"));
        }

        [Test]
        public void PostgreSqlColumn_PreservesCheckExpression_ThroughDeserializeAndSerializeAll()
        {
            const string json = """
            {"Name":"T","Schema":"public","Columns":[
                {"Name":"Quantity","DataType":"int4","CheckExpression":"Quantity > 0"}
            ]}
            """;
            var table = PlatformDeserializer.DeserializeTable(json, Platform.PostgreSQL);
            var roundTripped = JsonHelper.SerializeAll(table);
            Assert.That(roundTripped, Does.Contain("CheckExpression"));
            Assert.That(roundTripped, Does.Contain("Quantity > 0"));
        }

        [Test]
        public void PostgreSqlColumn_OmitsNullCheckExpression_FromSerializeAll()
        {
            const string json = """
            {"Name":"T","Schema":"public","Columns":[{"Name":"Id","DataType":"int4"}]}
            """;
            var table = PlatformDeserializer.DeserializeTable(json, Platform.PostgreSQL);
            Assert.That(JsonHelper.SerializeAll(table), Does.Not.Contain("CheckExpression"));
        }
    }
}

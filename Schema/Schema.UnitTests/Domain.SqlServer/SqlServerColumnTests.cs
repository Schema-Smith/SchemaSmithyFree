// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class SqlServerColumnTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromColumn()
        {
            var column = new SqlServerColumn();
            Assert.That(column, Is.InstanceOf<Column>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var column = new SqlServerColumn();

            Assert.That(column.Name, Is.EqualTo(""));
            Assert.That(column.DataType, Is.EqualTo(""));
            Assert.That(column.Nullable, Is.False);
            Assert.That(column.Default, Is.Null);
            Assert.That(column.CheckExpression, Is.Null);
            Assert.That(column.ComputedExpression, Is.Null);
            Assert.That(column.Persisted, Is.False);
            Assert.That(column.Sparse, Is.False);
            Assert.That(column.IsColumnSet, Is.False);
            Assert.That(column.Collation, Is.Null);
            Assert.That(column.DataMaskFunction, Is.Null);
            Assert.That(column.EncryptionType, Is.EqualTo("NONE"));
            Assert.That(column.EncryptionKey, Is.Null);
            Assert.That(column.EncryptionAlgorithm, Is.Null);
            Assert.That(column.ShouldApplyExpression, Is.Null);
            Assert.That(column.OldName, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var column = new SqlServerColumn
            {
                Name = "SSN",
                DataType = "varchar(11)",
                Nullable = true,
                Default = "'000-00-0000'",
                CheckExpression = "LEN([SSN]) = 11",
                ComputedExpression = null,
                Persisted = false,
                Sparse = true,
                IsColumnSet = false,
                Collation = "Latin1_General_CI_AS",
                DataMaskFunction = "partial(0,\"XXX-XX-\",4)",
                EncryptionType = "DETERMINISTIC",
                EncryptionKey = "CEK_SSN",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA_256",
                ShouldApplyExpression = "SELECT 1",
                OldName = "SocialSecurity"
            };

            var json = JsonConvert.SerializeObject(column);
            var deserialized = JsonConvert.DeserializeObject<SqlServerColumn>(json);

            Assert.That(deserialized.Name, Is.EqualTo("SSN"));
            Assert.That(deserialized.DataType, Is.EqualTo("varchar(11)"));
            Assert.That(deserialized.Nullable, Is.True);
            Assert.That(deserialized.Default, Is.EqualTo("'000-00-0000'"));
            Assert.That(deserialized.CheckExpression, Is.EqualTo("LEN([SSN]) = 11"));
            Assert.That(deserialized.Sparse, Is.True);
            Assert.That(deserialized.IsColumnSet, Is.False);
            Assert.That(deserialized.Collation, Is.EqualTo("Latin1_General_CI_AS"));
            Assert.That(deserialized.DataMaskFunction, Is.EqualTo("partial(0,\"XXX-XX-\",4)"));
            Assert.That(deserialized.EncryptionType, Is.EqualTo("DETERMINISTIC"));
            Assert.That(deserialized.EncryptionKey, Is.EqualTo("CEK_SSN"));
            Assert.That(deserialized.EncryptionAlgorithm, Is.EqualTo("AEAD_AES_256_CBC_HMAC_SHA_256"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
            Assert.That(deserialized.OldName, Is.EqualTo("SocialSecurity"));
        }

        [Test]
        public void JsonSerialization_OmitsDefaultAndNullProperties()
        {
            var column = new SqlServerColumn { Name = "Id", DataType = "int" };

            var json = JsonConvert.SerializeObject(column, NullIgnore);

            Assert.That(json, Does.Not.Contain("CheckExpression"));
            Assert.That(json, Does.Not.Contain("ComputedExpression"));
            Assert.That(json, Does.Not.Contain("Persisted"));
            Assert.That(json, Does.Not.Contain("Sparse"));
            Assert.That(json, Does.Not.Contain("IsColumnSet"));
            Assert.That(json, Does.Not.Contain("Collation"));
            Assert.That(json, Does.Not.Contain("DataMaskFunction"));
            Assert.That(json, Does.Not.Contain("EncryptionKey"));
            Assert.That(json, Does.Not.Contain("EncryptionAlgorithm"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var column = new SqlServerColumn { Name = "Id", DataType = "int" };
            var json = JsonConvert.SerializeObject(column);

            // PostgreSQL-specific
            Assert.That(json, Does.Not.Contain("Generated"));
            Assert.That(json, Does.Not.Contain("GenerationExpression"));
            Assert.That(json, Does.Not.Contain("Virtual"));
            Assert.That(json, Does.Not.Contain("Storage"));
            Assert.That(json, Does.Not.Contain("Compression"));
            // MySQL-specific
            Assert.That(json, Does.Not.Contain("AutoIncrement"));
            Assert.That(json, Does.Not.Contain("CharacterSet"));
            Assert.That(json, Does.Not.Contain("Comment"));
        }
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class PlatformJsonConverterTests
    {
        private class TestObject
        {
            [JsonConverter(typeof(PlatformJsonConverter))]
            public Platform Platform { get; set; }
        }

        [Test]
        public void Deserialize_Mssql_ReturnsSqlServer()
        {
            var json = """{"Platform":"MSSQL"}""";
            var result = JsonConvert.DeserializeObject<TestObject>(json);
            Assert.That(result.Platform, Is.EqualTo(Platform.SqlServer));
        }

        [Test]
        public void Deserialize_SqlServer_ReturnsSqlServer()
        {
            var json = """{"Platform":"SqlServer"}""";
            var result = JsonConvert.DeserializeObject<TestObject>(json);
            Assert.That(result.Platform, Is.EqualTo(Platform.SqlServer));
        }

        [Test]
        public void Deserialize_PostgreSQL_ReturnsPostgreSQL()
        {
            var json = """{"Platform":"PostgreSQL"}""";
            var result = JsonConvert.DeserializeObject<TestObject>(json);
            Assert.That(result.Platform, Is.EqualTo(Platform.PostgreSQL));
        }

        [Test]
        public void Deserialize_MySQL_ReturnsMySQL()
        {
            var json = """{"Platform":"MySQL"}""";
            var result = JsonConvert.DeserializeObject<TestObject>(json);
            Assert.That(result.Platform, Is.EqualTo(Platform.MySQL));
        }

        [TestCase("mysql", Platform.MySQL)]
        [TestCase("postgresql", Platform.PostgreSQL)]
        [TestCase("sqlserver", Platform.SqlServer)]
        [TestCase("mssql", Platform.SqlServer)]
        public void Deserialize_CaseInsensitive_ReturnsCorrectPlatform(string value, Platform expected)
        {
            var json = $"{{\"Platform\":\"{value}\"}}";
            var result = JsonConvert.DeserializeObject<TestObject>(json);
            Assert.That(result.Platform, Is.EqualTo(expected));
        }

        [TestCase(Platform.SqlServer, "SqlServer")]
        [TestCase(Platform.PostgreSQL, "PostgreSQL")]
        [TestCase(Platform.MySQL, "MySQL")]
        public void Serialize_WritesCanonicalName(Platform platform, string expected)
        {
            var obj = new TestObject { Platform = platform };
            var json = JsonConvert.SerializeObject(obj);
            Assert.That(json, Does.Contain($"\"{expected}\""));
            Assert.That(json, Does.Not.Contain("MSSQL"));
        }

        [Test]
        public void Deserialize_Null_ThrowsJsonSerializationException()
        {
            var json = """{"Platform":null}""";
            Assert.Throws<JsonSerializationException>(() =>
                JsonConvert.DeserializeObject<TestObject>(json));
        }

        [Test]
        public void Deserialize_UnknownValue_ThrowsJsonSerializationException()
        {
            var json = """{"Platform":"Oracle"}""";
            Assert.Throws<JsonSerializationException>(() =>
                JsonConvert.DeserializeObject<TestObject>(json));
        }
    }
}

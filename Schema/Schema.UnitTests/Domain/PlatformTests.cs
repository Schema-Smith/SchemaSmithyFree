// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class PlatformTests
    {
        [TestCase(Platform.SqlServer)]
        [TestCase(Platform.PostgreSQL)]
        [TestCase(Platform.MySQL)]
        public void GetBasePlatform_ReturnsSelft_ForAllBasePlatforms(Platform platform)
        {
            Assert.That(platform.GetBasePlatform(), Is.EqualTo(platform));
        }

        [Test]
        public void GetBasePlatform_ThrowsArgumentException_ForUnknown()
        {
            var ex = Assert.Throws<ArgumentException>(() => Platform.Unknown.GetBasePlatform());
            Assert.That(ex.Message, Does.Contain("Platform has not been assigned"));
        }

        [TestCase("MSSQL", Platform.SqlServer)]
        [TestCase("mssql", Platform.SqlServer)]
        [TestCase("Mssql", Platform.SqlServer)]
        public void ParsePlatform_AcceptsMssql_ReturnsSqlServer(string value, Platform expected)
        {
            Assert.That(PlatformExtensions.ParsePlatform(value), Is.EqualTo(expected));
        }

        [TestCase("SqlServer", Platform.SqlServer)]
        [TestCase("sqlserver", Platform.SqlServer)]
        [TestCase("SQLSERVER", Platform.SqlServer)]
        [TestCase("PostgreSQL", Platform.PostgreSQL)]
        [TestCase("postgresql", Platform.PostgreSQL)]
        [TestCase("MySQL", Platform.MySQL)]
        [TestCase("mysql", Platform.MySQL)]
        public void ParsePlatform_AcceptsCanonicalValues_CaseInsensitive(string value, Platform expected)
        {
            Assert.That(PlatformExtensions.ParsePlatform(value), Is.EqualTo(expected));
        }

        [Test]
        public void ParsePlatform_ThrowsArgumentException_ForUnknown()
        {
            var ex = Assert.Throws<ArgumentException>(() => PlatformExtensions.ParsePlatform("None"));
            Assert.That(ex.Message, Does.Contain("Unknown platform"));
        }

        [TestCase("Oracle")]
        [TestCase("MongoDB")]
        [TestCase("UnknownDB")]
        public void ParsePlatform_ThrowsArgumentException_ForUnknownValues(string value)
        {
            var ex = Assert.Throws<ArgumentException>(() => PlatformExtensions.ParsePlatform(value));
            Assert.That(ex.Message, Does.Contain("Unknown platform"));
            Assert.That(ex.Message, Does.Contain(value));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ParsePlatform_ThrowsArgumentException_ForNullOrEmpty(string value)
        {
            var ex = Assert.Throws<ArgumentException>(() => PlatformExtensions.ParsePlatform(value));
            Assert.That(ex.Message, Does.Contain("cannot be empty"));
        }

        [TestCase(Platform.SqlServer, "SqlServer")]
        [TestCase(Platform.PostgreSQL, "PostgreSQL")]
        [TestCase(Platform.MySQL, "MySQL")]
        public void ToCanonicalString_ReturnsEnumName(Platform platform, string expected)
        {
            Assert.That(platform.ToCanonicalString(), Is.EqualTo(expected));
        }

        [TestCase(Platform.SqlServer, "dbo")]
        [TestCase(Platform.PostgreSQL, "public")]
        [TestCase(Platform.MySQL, "")]
        public void GetDefaultSchema_ReturnsCorrectDefault_PerPlatform(Platform platform, string expected)
        {
            Assert.That(platform.GetDefaultSchema(), Is.EqualTo(expected));
        }
    }
}

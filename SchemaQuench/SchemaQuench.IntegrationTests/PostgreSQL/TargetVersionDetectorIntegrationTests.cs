// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.PostgreSQL
{
    [TestFixture]
    [Category("PostgreSQL")]
    public class TargetVersionDetectorIntegrationTests : BaseTableQuenchTests
    {
        [Test]
        public void Detect_ReturnsParseableComparable_AtOrAboveMinimumSupported()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var info = TargetVersionDetector.Detect(cmd, Platform.PostgreSQL);

            Assert.That(info.ServerComparable, Is.GreaterThanOrEqualTo(14)); // PostgreSQL 14 floor
        }
    }
}

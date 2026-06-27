// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.MySQL
{
    [TestFixture]
    [Category("MySQL")]
    public class TargetVersionDetectorIntegrationTests : BaseTableQuenchTests
    {
        [Test]
        public void Detect_ReturnsParseableComparable_AtOrAboveMinimumSupported()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var info = TargetVersionDetector.Detect(cmd, Platform.MySQL);

            Assert.That(info.ServerComparable, Is.GreaterThanOrEqualTo(800)); // MySQL 8.0 floor
        }
    }
}

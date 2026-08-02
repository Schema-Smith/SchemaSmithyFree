// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    [TestFixture]
    [Category("SqlServer")]
    public class TargetVersionDetectorIntegrationTests : BaseTableQuenchTests
    {
        [Test]
        public void Detect_ReturnsParseableComparable_AtOrAboveMinimumSupported()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var info = TargetVersionDetector.Detect(cmd, Platform.SqlServer);

            Assert.That(info.ServerComparable, Is.GreaterThanOrEqualTo(10)); // SQL Server 2008 (major 10) floor
        }
    }
}

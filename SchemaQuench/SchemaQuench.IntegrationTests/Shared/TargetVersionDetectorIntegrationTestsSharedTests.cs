// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.Shared;

public abstract class TargetVersionDetectorIntegrationTestsSharedTests : BaseTableQuenchTests
{
    [Test]
    public void Detect_ReturnsParseableComparable_AtOrAboveMinimumSupported()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        var info = TargetVersionDetector.Detect(cmd, Platform);

        // Floor is per-platform: MySQL 5.7 (507), MariaDB 10.2 (1002).
        Assert.That(info.ServerComparable, Is.GreaterThanOrEqualTo(VersionHelper.HardFloor(Platform)));
    }
}

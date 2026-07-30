// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility
{
    [TestFixture]
    public class VersionHelperTests
    {
        [TestCase("2017", Platform.SqlServer, 14)]
        [TestCase("2019", Platform.SqlServer, 15)]
        [TestCase("2022", Platform.SqlServer, 16)]
        [TestCase("15", Platform.SqlServer, 15)]   // already-a-major declaration
        [TestCase("16", Platform.PostgreSQL, 16)]
        [TestCase("15.3", Platform.PostgreSQL, 15)]
        [TestCase("8.0", Platform.MySQL, 800)]
        [TestCase("8.4", Platform.MySQL, 804)]
        [TestCase("8", Platform.MySQL, 800)]
        [TestCase("10.6", Platform.MariaDb, 1006)]
        [TestCase("11.4", Platform.MariaDb, 1104)]
        public void ParseDeclaredVersion_NormalizesToComparable(string version, Platform platform, int expected)
        {
            Assert.That(VersionHelper.ParseDeclaredVersion(version, platform), Is.EqualTo(expected));
        }

        [TestCase("not-a-version", Platform.SqlServer)]
        [TestCase("", Platform.PostgreSQL)]
        public void ParseDeclaredVersion_ReturnsNull_WhenUnparseable(string version, Platform platform)
        {
            Assert.That(VersionHelper.ParseDeclaredVersion(version, platform), Is.Null);
        }

        [TestCase("not-a-version", Platform.PostgreSQL)]
        [TestCase("", Platform.MySQL)]
        public void ParseDetectedVersion_ReturnsNull_WhenUnparseable(string raw, Platform platform)
        {
            Assert.That(VersionHelper.ParseDetectedVersion(raw, platform), Is.Null);
        }

        [TestCase("160004", Platform.PostgreSQL, 16)]
        [TestCase("150010", Platform.PostgreSQL, 15)]
        [TestCase("8.0.36", Platform.MySQL, 800)]
        [TestCase("8.4.1", Platform.MySQL, 804)]
        [TestCase("16", Platform.SqlServer, 16)]
        [TestCase("10.6.27-MariaDB", Platform.MariaDb, 1006)]
        [TestCase("11.4.2-MariaDB-1:11.4.2+maria~ubu2404", Platform.MariaDb, 1104)]
        public void ParseDetectedVersion_NormalizesToComparable(string raw, Platform platform, int expected)
        {
            Assert.That(VersionHelper.ParseDetectedVersion(raw, platform), Is.EqualTo(expected));
        }

        [TestCase(16, 17, false)]
        [TestCase(17, 17, true)]
        [TestCase(16, 15, true)]
        public void IsAtLeast_ComparesComparables(int detected, int required, bool expected)
        {
            Assert.That(VersionHelper.IsAtLeast(detected, required), Is.EqualTo(expected));
        }

        // 2016 (and older) is no longer a declarable SQL Server version — the real floor is 2017.
        [TestCase("2016", Platform.SqlServer)]
        [TestCase("2014", Platform.SqlServer)]
        public void ParseDeclaredVersion_BelowFloorYear_ReturnsNull(string version, Platform platform)
        {
            Assert.That(VersionHelper.ParseDeclaredVersion(version, platform), Is.Null);
        }

        [TestCase(Platform.SqlServer, 11, true)]    // SQL Server 2012
        [TestCase(Platform.SqlServer, 13, true)]    // SQL Server 2016
        [TestCase(Platform.SqlServer, 14, false)]   // 2017 floor
        [TestCase(Platform.SqlServer, 16, false)]
        [TestCase(Platform.PostgreSQL, 11, true)]   // PostgreSQL 11 (below floor)
        [TestCase(Platform.PostgreSQL, 12, false)]  // 12 floor
        [TestCase(Platform.MySQL, 507, true)]       // MySQL 5.7
        [TestCase(Platform.MySQL, 800, false)]      // 8.0 floor
        [TestCase(Platform.MariaDb, 1005, true)]    // MariaDB 10.5
        [TestCase(Platform.MariaDb, 1006, false)]   // 10.6 floor
        public void IsBelowFloor_ComparesAgainstEngineFloor(Platform platform, int comparable, bool expected)
        {
            Assert.That(VersionHelper.IsBelowFloor(platform, comparable), Is.EqualTo(expected));
        }

        [TestCase(Platform.SqlServer, "2017 (major 14)")]
        [TestCase(Platform.PostgreSQL, "12")]
        [TestCase(Platform.MySQL, "8.0")]
        [TestCase(Platform.MariaDb, "10.6")]
        public void HardFloorDisplay_MatchesSupportedFloorsTable(Platform platform, string expected)
        {
            Assert.That(VersionHelper.HardFloorDisplay(platform), Is.EqualTo(expected));
        }

        [TestCase(Platform.PostgreSQL, "160013", 16, "16")]   // raw server_version_num -> major
        [TestCase(Platform.SqlServer, "16", 16, "16")]
        [TestCase(Platform.MySQL, "8.0.36", 800, "8.0.36")]
        [TestCase(Platform.MariaDb, "10.6.27-MariaDB", 1006, "10.6.27-MariaDB")]
        public void DisplayVersion_NormalizesPostgresRawNum(Platform platform, string raw, int comparable, string expected)
        {
            var info = new TargetVersionInfo(platform, raw, comparable);
            Assert.That(VersionHelper.DisplayVersion(info), Is.EqualTo(expected));
        }
    }
}

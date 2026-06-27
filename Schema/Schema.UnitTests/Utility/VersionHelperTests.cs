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
        [TestCase("2016", Platform.SqlServer, 13)]
        [TestCase("15", Platform.SqlServer, 15)]   // already-a-major declaration
        [TestCase("16", Platform.PostgreSQL, 16)]
        [TestCase("15.3", Platform.PostgreSQL, 15)]
        [TestCase("8.0", Platform.MySQL, 800)]
        [TestCase("8.4", Platform.MySQL, 804)]
        [TestCase("8", Platform.MySQL, 800)]
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
    }
}

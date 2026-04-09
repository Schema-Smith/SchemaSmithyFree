// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class VersionHelperTests
{
    // ── Null/Empty MinimumVersion means no ceiling (all features available) ──

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void MeetsVersionThreshold_NullOrEmptyMinimumVersion_ReturnsTrue(string minimumVersion)
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(minimumVersion, "15", Platform.PostgreSQL), Is.True);
        Assert.That(VersionHelper.MeetsVersionThreshold(minimumVersion, "2019", Platform.SqlServer), Is.True);
        Assert.That(VersionHelper.MeetsVersionThreshold(minimumVersion, "8.0", Platform.MySQL), Is.True);
    }

    // ── PostgreSQL version comparison ──

    [TestCase("15", "15", true)]   // Exact match
    [TestCase("16", "15", true)]   // Above threshold
    [TestCase("14", "15", false)]  // Below threshold
    [TestCase("15", "14", true)]   // Above threshold
    [TestCase("17", "15", true)]   // Well above
    [TestCase("9", "15", false)]   // Well below
    public void MeetsVersionThreshold_PostgreSQL_ComparesCorrectly(string minimumVersion, string required, bool expected)
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(minimumVersion, required, Platform.PostgreSQL), Is.EqualTo(expected));
    }

    [Test]
    public void MeetsVersionThreshold_PostgreSQL_DottedVersion_UsesFirstComponent()
    {
        // "15.3" should be treated as 15
        Assert.That(VersionHelper.MeetsVersionThreshold("15.3", "15", Platform.PostgreSQL), Is.True);
        Assert.That(VersionHelper.MeetsVersionThreshold("14.9", "15", Platform.PostgreSQL), Is.False);
    }

    // ── SQL Server version comparison ──

    [TestCase("2019", "2019", true)]   // Exact match
    [TestCase("2022", "2019", true)]   // Above threshold
    [TestCase("2017", "2019", false)]  // Below threshold
    [TestCase("2019", "2017", true)]   // Above threshold
    public void MeetsVersionThreshold_SqlServer_ComparesCorrectly(string minimumVersion, string required, bool expected)
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(minimumVersion, required, Platform.SqlServer), Is.EqualTo(expected));
    }

    // ── MySQL version comparison ──

    [TestCase("8.0", "8.0", true)]    // Exact match
    [TestCase("8.4", "8.0", true)]    // Above threshold (minor)
    [TestCase("9.0", "8.0", true)]    // Above threshold (major)
    [TestCase("5.7", "8.0", false)]   // Below threshold
    [TestCase("8.0", "8.4", false)]   // Below by minor
    public void MeetsVersionThreshold_MySQL_ComparesCorrectly(string minimumVersion, string required, bool expected)
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(minimumVersion, required, Platform.MySQL), Is.EqualTo(expected));
    }

    [Test]
    public void MeetsVersionThreshold_MySQL_PlainMajorVersion_ComparesCorrectly()
    {
        // "8" should be treated as 8.0 (800)
        Assert.That(VersionHelper.MeetsVersionThreshold("8", "8.0", Platform.MySQL), Is.True);
        Assert.That(VersionHelper.MeetsVersionThreshold("9", "8.0", Platform.MySQL), Is.True);
        Assert.That(VersionHelper.MeetsVersionThreshold("5", "8.0", Platform.MySQL), Is.False);
    }

    // ── Unparseable versions default to allowing features ──

    [Test]
    public void MeetsVersionThreshold_UnparseableMinimumVersion_ReturnsTrue()
    {
        Assert.That(VersionHelper.MeetsVersionThreshold("abc", "15", Platform.PostgreSQL), Is.True);
    }

    [Test]
    public void MeetsVersionThreshold_UnparseableRequiredVersion_ReturnsTrue()
    {
        Assert.That(VersionHelper.MeetsVersionThreshold("15", "abc", Platform.PostgreSQL), Is.True);
    }

    // ── ParseVersion internal tests ──

    [TestCase("2019", Platform.SqlServer, 2019)]
    [TestCase("15", Platform.PostgreSQL, 15)]
    [TestCase("8.0", Platform.MySQL, 800)]
    [TestCase("8.4", Platform.MySQL, 804)]
    [TestCase("9.0", Platform.MySQL, 900)]
    [TestCase("5.7", Platform.MySQL, 507)]
    [TestCase("8", Platform.MySQL, 800)]
    [TestCase("15.3", Platform.PostgreSQL, 15)]
    [TestCase(null, Platform.SqlServer, null)]
    [TestCase("", Platform.SqlServer, null)]
    public void ParseVersion_ReturnsExpectedValue(string version, Platform platform, int? expected)
    {
        Assert.That(VersionHelper.ParseVersion(version, platform), Is.EqualTo(expected));
    }
}

using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests;

public class VersionHelperTests
{
    [Test]
    public void ShouldReturnTrueWhenMinimumVersionIsNull()
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(null, SqlServerVersion.Sql2019), Is.True);
    }

    [TestCase(SqlServerVersion.Sql2019, SqlServerVersion.Sql2019, true)]
    [TestCase(SqlServerVersion.Sql2022, SqlServerVersion.Sql2019, true)]
    [TestCase(SqlServerVersion.Sql2017, SqlServerVersion.Sql2019, false)]
    [TestCase(SqlServerVersion.Sql2019, SqlServerVersion.Sql2017, true)]
    [TestCase(SqlServerVersion.Sql2025, SqlServerVersion.Sql2016, true)]
    [TestCase(SqlServerVersion.Sql2016, SqlServerVersion.Sql2025, false)]
    public void ShouldCompareVersionsCorrectly(SqlServerVersion minimum, SqlServerVersion required, bool expected)
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(minimum, required), Is.EqualTo(expected));
    }

    [Test]
    public void ShouldReturnTrueForLowestVersionAgainstItself()
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(SqlServerVersion.Sql2016, SqlServerVersion.Sql2016), Is.True);
    }

    [Test]
    public void ShouldReturnTrueForHighestVersionAgainstItself()
    {
        Assert.That(VersionHelper.MeetsVersionThreshold(SqlServerVersion.Sql2025, SqlServerVersion.Sql2025), Is.True);
    }
}

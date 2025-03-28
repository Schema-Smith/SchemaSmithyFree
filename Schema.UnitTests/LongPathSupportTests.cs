using Schema.Utility;

namespace Schema.UnitTests;

public class LongPathSupportTests
{
    [Test]
    public void ShouldNotModifyPathForLongFileNamesOnLinux()
    {
        var newPath = LongPathSupport.MakeSafeLongFilePath("path", true);
        Assert.That(newPath, Is.EqualTo("path"));
    }

    [Test]
    public void ShouldNotModifyPathIfAlreadyPrefixedForLongPathSupport()
    {
        var newPath = LongPathSupport.MakeSafeLongFilePath(@"\\?\path", false);
        Assert.That(newPath, Is.EqualTo(@"\\?\path"));
    }

    [Test]
    public void ShouldProperlyHandleLongFileNamePatchForUNCPathOnWindows()
    {
        var newPath = LongPathSupport.MakeSafeLongFilePath(@"\\path", false);
        Assert.That(newPath, Is.EqualTo(@"\\?\UNC\path"));
    }

    [Test]
    public void ShouldProperlyHandleLongFileNamePatchForNonUNCPathOnWindows()
    {
        var newPath = LongPathSupport.MakeSafeLongFilePath(@"C:\path", false);
        Assert.That(newPath, Is.EqualTo(@"\\?\C:\path"));
    }
}

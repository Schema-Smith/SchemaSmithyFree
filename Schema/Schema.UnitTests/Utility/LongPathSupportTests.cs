// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class LongPathSupportTests
{
    // The \\?\ long-path prefix is only valid on a fully-qualified path. Prefixing a RELATIVE
    // path (e.g. "Package\Product.json") produces "\\?\Package\Product.json", which the Windows
    // file APIs treat as non-existent — the root cause of relative --Source/Product:Path failing
    // for SchemaShears and SchemaTongs. overrideIsLinux:false exercises the Windows logic on any
    // runtime so this is covered on the Linux CI too.

    [Test]
    public void RelativePathWithBackslashSeparator_IsNotGivenLongPathPrefix()
    {
        var result = LongPathSupport.MakeSafeLongFilePath(@"Package\Product.json", overrideIsLinux: false);

        Assert.That(result, Does.Not.StartWith(@"\\?\"), "a relative path must not receive the \\\\?\\ prefix");
        Assert.That(result, Is.EqualTo(@"Package\Product.json"));
    }

    [Test]
    public void RelativePathWithForwardSlashSeparator_IsNotGivenLongPathPrefix()
    {
        var result = LongPathSupport.MakeSafeLongFilePath("sub/dir/file.json", overrideIsLinux: false);

        Assert.That(result, Does.Not.StartWith(@"\\?\"));
    }

    [Test]
    public void DriveRelativePath_IsNotGivenLongPathPrefix()
    {
        // "C:file" is drive-relative (no root separator) — also not fully qualified.
        var result = LongPathSupport.MakeSafeLongFilePath(@"C:file.json", overrideIsLinux: false);

        Assert.That(result, Does.Not.StartWith(@"\\?\"));
    }

    [Test]
    public void AbsoluteDrivePath_GetsLongPathPrefix()
    {
        var result = LongPathSupport.MakeSafeLongFilePath(@"C:\dir\file.json", overrideIsLinux: false);

        Assert.That(result, Is.EqualTo(@"\\?\C:\dir\file.json"));
    }

    [Test]
    public void UncPath_GetsUncLongPathPrefix()
    {
        var result = LongPathSupport.MakeSafeLongFilePath(@"\\server\share\file.txt", overrideIsLinux: false);

        Assert.That(result, Is.EqualTo(@"\\?\UNC\server\share\file.txt"));
    }

    [Test]
    public void AlreadyPrefixedPath_IsUnchanged()
    {
        var result = LongPathSupport.MakeSafeLongFilePath(@"\\?\C:\dir\file.json", overrideIsLinux: false);

        Assert.That(result, Is.EqualTo(@"\\?\C:\dir\file.json"));
    }

    [Test]
    public void DotRelativePath_IsUnchanged()
    {
        var result = LongPathSupport.MakeSafeLongFilePath(@".\Package\Product.json", overrideIsLinux: false);

        Assert.That(result, Is.EqualTo(@".\Package\Product.json"));
    }

    [Test]
    public void PathWithoutSeparators_IsUnchanged()
    {
        var result = LongPathSupport.MakeSafeLongFilePath("Package", overrideIsLinux: false);

        Assert.That(result, Is.EqualTo("Package"));
    }

    [Test]
    public void LinuxMode_IsUnchanged()
    {
        var result = LongPathSupport.MakeSafeLongFilePath(@"C:\dir\file.json", overrideIsLinux: true);

        Assert.That(result, Is.EqualTo(@"C:\dir\file.json"));
    }
}

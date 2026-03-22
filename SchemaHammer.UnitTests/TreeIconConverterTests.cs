using System.Globalization;
using SchemaHammer.Converters;

namespace SchemaHammer.UnitTests;

public class TreeIconConverterTests
{
    [Test]
    public void Instance_IsNotNull()
    {
        Assert.That(TreeIconConverter.Instance, Is.Not.Null);
    }

    [Test]
    public void Convert_WithFewerThanTwoValues_ReturnsNull()
    {
        var converter = TreeIconConverter.Instance;
        var result = converter.Convert(
            new List<object?> { "folder" },
            typeof(object),
            null,
            CultureInfo.InvariantCulture);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Convert_WithEmptyValues_ReturnsNull()
    {
        var converter = TreeIconConverter.Instance;
        var result = converter.Convert(
            new List<object?> { },
            typeof(object),
            null,
            CultureInfo.InvariantCulture);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Convert_WithNullApplicationCurrent_ReturnsNull()
    {
        // Application.Current is null in test context, geometry lookup returns null
        var converter = TreeIconConverter.Instance;
        var result = converter.Convert(
            new List<object?> { "folder", "Table" },
            typeof(object),
            null,
            CultureInfo.InvariantCulture);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Convert_WithNullImageKeyAndNullTag_ReturnsNull()
    {
        // Both values null — imageKey defaults to "folder", tag to ""; Application.Current is null
        var converter = TreeIconConverter.Instance;
        var result = converter.Convert(
            new List<object?> { null, null },
            typeof(object),
            null,
            CultureInfo.InvariantCulture);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Convert_WithKnownTag_ReturnsNullWhenNoApplicationCurrent()
    {
        // All known tags should return null when Application.Current is absent
        var converter = TreeIconConverter.Instance;
        var knownTags = new[]
        {
            "Product", "Template", "Table", "Column", "Index",
            "Xml Index", "Foreign Key", "Check Constraint",
            "Statistic", "Full Text Index", "Indexed View", "Sql Script"
        };

        foreach (var tag in knownTags)
        {
            var result = converter.Convert(
                new List<object?> { "folder", tag },
                typeof(object),
                null,
                CultureInfo.InvariantCulture);
            Assert.That(result, Is.Null, $"Expected null for tag '{tag}' with no Application.Current");
        }
    }

    [Test]
    public void Convert_WithFileImageKey_ReturnsNull()
    {
        var converter = TreeIconConverter.Instance;
        var result = converter.Convert(
            new List<object?> { "file", "Unknown" },
            typeof(object),
            null,
            CultureInfo.InvariantCulture);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Convert_WithProductImageKey_ReturnsNull()
    {
        var converter = TreeIconConverter.Instance;
        var result = converter.Convert(
            new List<object?> { "product", "" },
            typeof(object),
            null,
            CultureInfo.InvariantCulture);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Convert_WithTemplateImageKey_ReturnsNull()
    {
        var converter = TreeIconConverter.Instance;
        var result = converter.Convert(
            new List<object?> { "template", "" },
            typeof(object),
            null,
            CultureInfo.InvariantCulture);
        Assert.That(result, Is.Null);
    }
}

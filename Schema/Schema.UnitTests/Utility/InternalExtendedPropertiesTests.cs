// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class InternalExtendedPropertiesTests
{
    [Test]
    public void Names_ContainsProductName()
    {
        Assert.That(InternalExtendedProperties.Names, Does.Contain("ProductName"));
    }

    [Test]
    public void IsInternal_ProductName_ReturnsTrue()
    {
        Assert.That(InternalExtendedProperties.IsInternal("ProductName"), Is.True);
    }

    [TestCase("productname")]
    [TestCase("PRODUCTNAME")]
    [TestCase("productName")]
    public void IsInternal_CaseInsensitive(string name)
    {
        Assert.That(InternalExtendedProperties.IsInternal(name), Is.True);
    }

    [TestCase("MS_Description")]
    [TestCase("SomeOtherProp")]
    [TestCase("")]
    public void IsInternal_NonInternalNames_ReturnsFalse(string name)
    {
        Assert.That(InternalExtendedProperties.IsInternal(name), Is.False);
    }

    [Test]
    public void SqlExclusionFilter_ContainsProductName()
    {
        Assert.That(InternalExtendedProperties.SqlExclusionFilter, Does.Contain("N'ProductName'"));
    }
}

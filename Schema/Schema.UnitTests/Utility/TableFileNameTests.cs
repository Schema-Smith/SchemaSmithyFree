// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class TableFileNameTests
{
    [Test]
    public void Canonical_NoVariant_IsSchemaDotTableDotJson()
    {
        Assert.That(TableFileName.Canonical("dbo", "Orders", "", false), Is.EqualTo("dbo.Orders.json"));
    }

    [Test]
    public void Canonical_WithVariant_AppendsVariantAfterSchemaAndTable()
    {
        Assert.That(TableFileName.Canonical("dbo", "Orders", "EU", false), Is.EqualTo("dbo.Orders.EU.json"));
    }

    [Test]
    public void Canonical_SchemaTemplate_OmitsSchemaSegment()
    {
        Assert.That(TableFileName.Canonical("dbo", "Orders", "EU", true), Is.EqualTo("Orders.EU.json"));
    }

    [Test]
    public void Canonical_SchemaTemplate_NoVariant_IsTableDotJson()
    {
        Assert.That(TableFileName.Canonical("dbo", "Orders", "", true), Is.EqualTo("Orders.json"));
    }

    [Test]
    public void Canonical_BlankOrWhitespaceVariant_TreatedAsNoVariant()
    {
        Assert.That(TableFileName.Canonical("dbo", "Orders", "   ", false), Is.EqualTo("dbo.Orders.json"));
    }

    [Test]
    public void Canonical_TrimsVariantBeforeEncoding()
    {
        Assert.That(TableFileName.Canonical("dbo", "Orders", "  EU  ", false), Is.EqualTo("dbo.Orders.EU.json"));
    }

    [Test]
    public void Canonical_EncodesEachSegmentWithFileNameEncoder()
    {
        var expected = $"{FileNameEncoder.Encode("dbo")}.{FileNameEncoder.Encode("Or/ders")}.{FileNameEncoder.Encode("E<U")}.json";
        Assert.That(TableFileName.Canonical("dbo", "Or/ders", "E<U", false), Is.EqualTo(expected));
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class DropSuppressionStampTests
{
    private string _dir;
    private string _productJson;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "shears-suppress-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _productJson = Path.Combine(_dir, "Product.json");
        File.WriteAllText(_productJson, "{ \"Name\": \"Acme\", \"SomeFutureUnknownProperty\": 7 }");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    [Test]
    public void Apply_EmptyAllowDrops_StampsAllSevenFlagsFalse()
    {
        DropSuppressionStamp.Apply(_productJson, Array.Empty<string>());

        var json = JObject.Parse(File.ReadAllText(_productJson));
        Assert.Multiple(() =>
        {
            Assert.That(json["DropTablesRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropColumnsRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropUnknownIndexes"]!.Value<bool>(), Is.False);
            Assert.That(json["DropForeignKeysRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropCheckConstraintsRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropExcludeConstraintsRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropStatisticsRemovedFromProduct"]!.Value<bool>(), Is.False);
        });
    }

    [Test]
    [TestCase("Columns")]
    [TestCase("columns")]
    public void Apply_AllowColumns_OmitsColumnsFlag_StampsOtherSixFalse(string allowedCategory)
    {
        DropSuppressionStamp.Apply(_productJson, new[] { allowedCategory });

        var json = JObject.Parse(File.ReadAllText(_productJson));
        Assert.Multiple(() =>
        {
            Assert.That(json["DropColumnsRemovedFromProduct"], Is.Null,
                "DropColumnsRemovedFromProduct should NOT be stamped when Columns is in allowDrops");

            Assert.That(json["DropTablesRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropUnknownIndexes"]!.Value<bool>(), Is.False);
            Assert.That(json["DropForeignKeysRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropCheckConstraintsRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropExcludeConstraintsRemovedFromProduct"]!.Value<bool>(), Is.False);
            Assert.That(json["DropStatisticsRemovedFromProduct"]!.Value<bool>(), Is.False);
        });
    }

    [Test]
    public void Apply_PreservesExistingUnrelatedProperties()
    {
        DropSuppressionStamp.Apply(_productJson, Array.Empty<string>());

        var json = JObject.Parse(File.ReadAllText(_productJson));
        Assert.Multiple(() =>
        {
            Assert.That(json["Name"]!.Value<string>(), Is.EqualTo("Acme"));
            Assert.That(json["SomeFutureUnknownProperty"]!.Value<int>(), Is.EqualTo(7));
        });
    }

    [Test]
    public void Apply_UnknownCategory_ThrowsPatchBuildException()
    {
        var ex = Assert.Throws<PatchBuildException>(() =>
            DropSuppressionStamp.Apply(_productJson, new[] { "Widgets" }));
        Assert.That(ex!.Message, Does.Contain("Widgets"));
    }

    [Test]
    public void Apply_MissingFile_ThrowsPatchBuildException()
    {
        Assert.Throws<PatchBuildException>(() =>
            DropSuppressionStamp.Apply(Path.Combine(_dir, "nope.json"), Array.Empty<string>()));
    }
}

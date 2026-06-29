// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class DropTablesRemovedFromProductStampTests
{
    private string _dir;
    private string _productJson;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "shears-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _productJson = Path.Combine(_dir, "Product.json");
        File.WriteAllText(_productJson, "{ \"Name\": \"Acme\", \"SomeFutureUnknownProperty\": 7 }");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    [Test]
    public void Apply_SetsFlagFalse_AndPreservesExistingProperties()
    {
        DropTablesRemovedFromProductStamp.Apply(_productJson);

        var json = JObject.Parse(File.ReadAllText(_productJson));
        Assert.Multiple(() =>
        {
            Assert.That(json["DropTablesRemovedFromProduct"].Value<bool>(), Is.False);
            Assert.That(json["Name"].Value<string>(), Is.EqualTo("Acme"));
            Assert.That(json["SomeFutureUnknownProperty"].Value<int>(), Is.EqualTo(7));
        });
    }

    [Test]
    public void Apply_MissingFile_Throws()
    {
        Assert.Throws<PatchBuildException>(() => DropTablesRemovedFromProductStamp.Apply(Path.Combine(_dir, "nope.json")));
    }
}

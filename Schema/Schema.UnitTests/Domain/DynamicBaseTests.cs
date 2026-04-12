// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain;

[TestFixture]
public class DynamicBaseExtensionPropertyTests
{
    [Test]
    public void GetExtensionProperty_WhenExtensionsNull_ReturnsNull()
    {
        var table = new SqlServerTable();
        Assert.That(table.GetExtensionProperty("Anything"), Is.Null);
    }

    [Test]
    public void GetExtensionProperty_WhenPropertyExists_ReturnsValue()
    {
        var table = new SqlServerTable
        {
            Extensions = new JObject { ["Description"] = "Test" }
        };
        Assert.That(table.GetExtensionProperty("Description")?.ToString(), Is.EqualTo("Test"));
    }

    [Test]
    public void GetExtensionProperty_WhenPropertyMissing_ReturnsNull()
    {
        var table = new SqlServerTable
        {
            Extensions = new JObject { ["Other"] = "Value" }
        };
        Assert.That(table.GetExtensionProperty("Missing"), Is.Null);
    }

    [Test]
    public void SetExtensionProperty_WhenExtensionsNull_CreatesJObjectAndSets()
    {
        var table = new SqlServerTable();
        table.SetExtensionProperty("Description", "Hello");

        Assert.That(table.Extensions, Is.Not.Null);
        Assert.That((table.Extensions as JObject)?["Description"]?.ToString(), Is.EqualTo("Hello"));
    }

    [Test]
    public void SetExtensionProperty_WhenExtensionsExists_SetsValue()
    {
        var table = new SqlServerTable
        {
            Extensions = new JObject { ["Existing"] = "Keep" }
        };
        table.SetExtensionProperty("New", "Added");

        Assert.That((table.Extensions as JObject)?["Existing"]?.ToString(), Is.EqualTo("Keep"));
        Assert.That((table.Extensions as JObject)?["New"]?.ToString(), Is.EqualTo("Added"));
    }

    [Test]
    public void SetExtensionProperty_OverwritesExistingValue()
    {
        var table = new SqlServerTable
        {
            Extensions = new JObject { ["Description"] = "Old" }
        };
        table.SetExtensionProperty("Description", "New");

        Assert.That((table.Extensions as JObject)?["Description"]?.ToString(), Is.EqualTo("New"));
    }

    [Test]
    public void SetExtensionProperty_NullValue_SetsNull()
    {
        var table = new SqlServerTable
        {
            Extensions = new JObject { ["Description"] = "Value" }
        };
        table.SetExtensionProperty("Description", null);

        Assert.That((table.Extensions as JObject)?["Description"]?.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void SetExtensionProperty_ComplexObject_SetsAsJToken()
    {
        var table = new SqlServerTable();
        var nested = new JObject { ["Key"] = "Value" };
        table.SetExtensionProperty("Config", nested);

        var result = (table.Extensions as JObject)?["Config"];
        Assert.That(result, Is.Not.Null);
        Assert.That(result["Key"]?.ToString(), Is.EqualTo("Value"));
    }

    [Test]
    public void GetExtensionProperty_BooleanValue_ReturnsBool()
    {
        var table = new SqlServerTable
        {
            Extensions = new JObject { ["IsActive"] = true }
        };
        var result = table.GetExtensionProperty("IsActive");
        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void RoundTrip_SetThenGet_ReturnsValue()
    {
        var table = new SqlServerTable();
        table.SetExtensionProperty("MyProp", "RoundTripped");

        Assert.That(table.GetExtensionProperty("MyProp")?.ToString(), Is.EqualTo("RoundTripped"));
    }
}

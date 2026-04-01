// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class TemplateRequiredTests
{
    [Test]
    public void Required_DefaultsToTrue()
    {
        var template = new Template();
        Assert.That(template.Required, Is.True);
    }

    [Test]
    public void SkipIfReadOnly_DefaultsToFalse()
    {
        var template = new Template();
        Assert.That(template.SkipIfReadOnly, Is.False);
    }

    [Test]
    public void Required_DeserializesFromJson()
    {
        var json = """{ "Name": "T1", "DatabaseIdentificationScript": "SELECT 'db1'", "Required": false }""";
        var template = JsonConvert.DeserializeObject<Template>(json);
        Assert.That(template.Required, Is.False);
    }

    [Test]
    public void SkipIfReadOnly_DeserializesFromJson()
    {
        var json = """{ "Name": "T1", "DatabaseIdentificationScript": "SELECT 'db1'", "SkipIfReadOnly": true }""";
        var template = JsonConvert.DeserializeObject<Template>(json);
        Assert.That(template.SkipIfReadOnly, Is.True);
    }

    [Test]
    public void Required_RoundTrip_PreservesValue()
    {
        var template = new Template { Name = "T1", DatabaseIdentificationScript = "SELECT 1", Required = false };
        var json = JsonConvert.SerializeObject(template);
        var rt = JsonConvert.DeserializeObject<Template>(json);
        Assert.That(rt.Required, Is.False);
    }

    [Test]
    public void MissingRequiredInJson_DefaultsToTrue()
    {
        var json = """{ "Name": "T1", "DatabaseIdentificationScript": "SELECT 'db1'" }""";
        var template = JsonConvert.DeserializeObject<Template>(json);
        Assert.That(template.Required, Is.True);
    }
}

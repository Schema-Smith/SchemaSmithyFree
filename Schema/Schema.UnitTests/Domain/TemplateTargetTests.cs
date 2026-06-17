// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using NUnit.Framework;
using Schema.Domain;

namespace Schema.UnitTests.Domain;

[TestFixture]
public class TemplateTargetTests
{
    [Test]
    public void Defaults_AreSensible()
    {
        var target = new TemplateTarget();
        Assert.That(target.Databases, Is.Empty);
        Assert.That(target.Schemas, Is.Empty);
        Assert.That(target.CreateIfMissing, Is.False);
    }

    [Test]
    public void HasNoTargets_ReturnsTrueForEmptyEntry()
    {
        var target = new TemplateTarget();
        Assert.That(target.HasNoTargets, Is.True);
    }

    [Test]
    public void HasNoTargets_ReturnsFalseWhenDatabasesPresent()
    {
        var target = new TemplateTarget { Databases = new List<string> { "acme_db" } };
        Assert.That(target.HasNoTargets, Is.False);
    }

    [Test]
    public void HasNoTargets_ReturnsFalseWhenSchemasPresent()
    {
        var target = new TemplateTarget { Schemas = new List<string> { "acme" } };
        Assert.That(target.HasNoTargets, Is.False);
    }
}

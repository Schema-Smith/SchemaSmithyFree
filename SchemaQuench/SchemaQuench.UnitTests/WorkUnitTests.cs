// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class WorkUnitTests
{
    [Test]
    public void Constructor_RegularTemplate_EmptySchemaName()
    {
        var unit = new WorkUnit("server1", "AppDb", "Core", "");

        Assert.Multiple(() =>
        {
            Assert.That(unit.Server, Is.EqualTo("server1"));
            Assert.That(unit.DatabaseName, Is.EqualTo("AppDb"));
            Assert.That(unit.TemplateName, Is.EqualTo("Core"));
            Assert.That(unit.SchemaName, Is.EqualTo(""));
        });
    }

    [Test]
    public void Constructor_SchemaTemplate_CarriesSchemaName()
    {
        var unit = new WorkUnit("server1", "AppDb", "TenantBody", "tenant_acme");

        Assert.That(unit.SchemaName, Is.EqualTo("tenant_acme"));
    }

    [Test]
    public void Equality_AllFieldsMatch_AreEqual()
    {
        var a = new WorkUnit("s", "db", "tmpl", "schema");
        var b = new WorkUnit("s", "db", "tmpl", "schema");

        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void Equality_DifferentSchema_NotEqual()
    {
        var a = new WorkUnit("s", "db", "tmpl", "tenant_a");
        var b = new WorkUnit("s", "db", "tmpl", "tenant_b");

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void Equality_RegularVsSchemaIteration_NotEqual()
    {
        var regular = new WorkUnit("s", "db", "tmpl", "");
        var schemaIter = new WorkUnit("s", "db", "tmpl", "tenant_a");

        Assert.That(regular, Is.Not.EqualTo(schemaIter));
    }

    [Test]
    public void Equality_DifferentTemplate_NotEqual()
    {
        var a = new WorkUnit("s", "db", "Core", "");
        var b = new WorkUnit("s", "db", "Reports", "");

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void Equality_DifferentDatabase_NotEqual()
    {
        var a = new WorkUnit("s", "AppA", "Core", "");
        var b = new WorkUnit("s", "AppB", "Core", "");

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void Equality_DifferentServer_NotEqual()
    {
        var a = new WorkUnit("s1", "db", "Core", "");
        var b = new WorkUnit("s2", "db", "Core", "");

        Assert.That(a, Is.Not.EqualTo(b));
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Checkpointing;

namespace Schema.UnitTests.Checkpointing;

[TestFixture]
public class TrackingScopeTests
{
    [Test]
    public void NewScope_SchemaName_DefaultsToEmptyString()
    {
        var scope = new TrackingScope
        {
            ProductName = "P",
            TemplateName = "T",
            Server = "s",
            DatabaseName = "d"
        };
        Assert.That(scope.SchemaName, Is.EqualTo(""));
    }

    [Test]
    public void CompositeKey_IncludesAllFiveDimensions_InStableOrder()
    {
        var scope = new TrackingScope
        {
            ProductName = "Demo",
            TemplateName = "TenantBody",
            Server = "srv1",
            DatabaseName = "AppProd",
            SchemaName = "tenant_acme"
        };
        Assert.That(scope.CompositeKey, Is.EqualTo("Demo|TenantBody|srv1|AppProd|tenant_acme"));
    }

    [Test]
    public void CompositeKey_RegularTemplateScope_HasEmptySchemaSegment()
    {
        var scope = new TrackingScope
        {
            ProductName = "Demo",
            TemplateName = "Shared",
            Server = "srv1",
            DatabaseName = "AppProd"
        };
        Assert.That(scope.CompositeKey, Is.EqualTo("Demo|Shared|srv1|AppProd|"));
    }

    [Test]
    public void TwoScopes_DifferingOnlyBySchema_ProduceDistinctCompositeKeys()
    {
        var a = new TrackingScope
        {
            ProductName = "P", TemplateName = "T", Server = "s", DatabaseName = "d",
            SchemaName = "tenant_a"
        };
        var b = new TrackingScope
        {
            ProductName = "P", TemplateName = "T", Server = "s", DatabaseName = "d",
            SchemaName = "tenant_b"
        };
        Assert.That(a.CompositeKey, Is.Not.EqualTo(b.CompositeKey));
    }

    [Test]
    public void TwoScopes_LegacyVsNewTenant_DistinctCompositeKeys()
    {
        // Legacy tracking row (pre-extension): no schema name.
        var legacy = new TrackingScope
        {
            ProductName = "Demo", TemplateName = "TenantBody",
            Server = "srv1", DatabaseName = "AppProd"
        };
        // New per-tenant tracking row.
        var perTenant = new TrackingScope
        {
            ProductName = "Demo", TemplateName = "TenantBody",
            Server = "srv1", DatabaseName = "AppProd",
            SchemaName = "tenant_acme"
        };
        // Critical: legacy and per-tenant rows must NOT collide in the scope key
        // (otherwise legacy blank-schema rows would shadow per-tenant lookups).
        Assert.That(legacy.CompositeKey, Is.Not.EqualTo(perTenant.CompositeKey));
    }
}

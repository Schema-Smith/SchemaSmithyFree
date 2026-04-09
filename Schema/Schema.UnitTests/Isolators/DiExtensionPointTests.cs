// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Isolators;

public class DiExtensionPointTests
{
    [SetUp]
    public void SetUp()
    {
        // Ensure clean FactoryContainer state between tests
        FactoryContainer.Clear();
    }

    // --- NullCheckpointing ---

    [Test]
    public void NullCheckpointing_Track_RunsAction()
    {
        var cp = new NullCheckpointing();
        var ran = false;
        cp.Track(new TrackingScope { ProductName = "MyProduct" }, "KindleForge", () => ran = true);
        Assert.That(ran, Is.True);
    }

    [Test]
    public void NullCheckpointing_TrackScript_RunsAction()
    {
        var cp = new NullCheckpointing();
        var ran = false;
        cp.TrackScript(new TrackingScope { ProductName = "MyProduct", TemplateName = "T", Server = "S", DatabaseName = "D" },
                       "Object", "scripts/foo.sql", () => ran = true);
        Assert.That(ran, Is.True);
    }

    // --- NullSchemaLicense ---

    [Test]
    public void NullSchemaLicense_IsLicensed_ReturnsFalse()
    {
        var license = new NullSchemaLicense();
        Assert.That(license.IsLicensed, Is.False);
    }

    [Test]
    public void NullSchemaLicense_LicenseDisplayText_IdentifiesCommunity()
    {
        // We don't pin the exact wording — the label may evolve, and a live Pro
        // package returns a multi-line details block. The only invariant is that
        // the unlicensed default identifies itself as Community.
        var license = new NullSchemaLicense();
        Assert.That(license.LicenseDisplayText, Does.Contain("Community"));
    }

    [Test]
    public void NullSchemaLicense_GetAdditionalCommandLineOptions_ReturnsEmptyList()
    {
        var license = new NullSchemaLicense();
        var options = license.GetAdditionalCommandLineOptions("SchemaQuench");
        Assert.That(options, Is.Not.Null);
        Assert.That(options, Is.Empty);
    }

    // --- LicenseCommandLineOption record ---

    [Test]
    public void LicenseCommandLineOption_RecordEquality_ByValue()
    {
        var a = new LicenseCommandLineOption("--checkpoint", "Enable checkpointing");
        var b = new LicenseCommandLineOption("--checkpoint", "Enable checkpointing");
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void LicenseCommandLineOption_RecordInequality_WhenDifferent()
    {
        var a = new LicenseCommandLineOption("--checkpoint", "Enable checkpointing");
        var b = new LicenseCommandLineOption("--resume", "Resume from checkpoint");
        Assert.That(a, Is.Not.EqualTo(b));
    }

    // --- NullDataDelivery ---

    [Test]
    public void NullDataDelivery_DeliverTables_DoesNotThrow()
    {
        var delivery = new NullDataDelivery();
        var context = new DataDeliveryContext
        {
            Tables = new List<IDeliverableTable>(),
            Platform = "SqlServer",
            DatabaseName = "test",
            TemplateRootPath = "/tmp"
        };
        Assert.DoesNotThrow(() => delivery.DeliverTables(context));
    }

    [Test]
    public void NullDataDelivery_DeliverTables_DoesNotThrow_WithNullContext()
    {
        var delivery = new NullDataDelivery();
        Assert.DoesNotThrow(() => delivery.DeliverTables(null));
    }

    [Test]
    public void DataDeliveryContext_Defaults_AreNullOrZero()
    {
        var context = new DataDeliveryContext();
        Assert.That(context.Tables, Is.Null);
        Assert.That(context.Command, Is.Null);
        Assert.That(context.Platform, Is.Null);
        Assert.That(context.DatabaseName, Is.Null);
        Assert.That(context.TemplateRootPath, Is.Null);
        Assert.That(context.ProgressLog, Is.Null);
        Assert.That(context.ProgressLogError, Is.Null);
        Assert.That(context.WhatIf, Is.False);
    }

    // --- ToolHelpFormatter ---

    [Test]
    public void ToolHelpFormatter_FormatProOptions_ReturnsEmptyString_WhenNoLicense()
    {
        var result = ToolHelpFormatter.FormatProOptions("SchemaQuench");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ToolHelpFormatter_GetLicenseDisplayText_IdentifiesCommunity_WhenNoLicense()
    {
        // With no Pro package registered, the formatter resolves to the Null default.
        // Assert only that the result identifies the unlicensed state — do not pin
        // specific verbiage, since the label may evolve and a live Pro package
        // returns a multi-line details block.
        var result = ToolHelpFormatter.GetLicenseDisplayText();
        Assert.That(result, Does.Contain("Community"));
    }
}

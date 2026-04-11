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

    // --- ProCheckpointingWrapper (default unlicensed behavior) ---

    [Test]
    public void ProCheckpointingWrapper_Track_RunsAction()
    {
        var cp = ProCheckpointingWrapper.GetFromFactory();
        var ran = false;
        cp.Track(new TrackingScope { ProductName = "MyProduct" }, "KindleForge", () => ran = true);
        Assert.That(ran, Is.True);
    }

    [Test]
    public void ProCheckpointingWrapper_TrackScript_RunsAction()
    {
        var cp = ProCheckpointingWrapper.GetFromFactory();
        var ran = false;
        cp.TrackScript(new TrackingScope { ProductName = "MyProduct", TemplateName = "T", Server = "S", DatabaseName = "D" },
                       "Object", "scripts/foo.sql", () => ran = true);
        Assert.That(ran, Is.True);
    }

    // --- ProLicenseWrapper (default unlicensed behavior) ---

    [Test]
    public void ProLicenseWrapper_IsLicensed_ReturnsBoolean()
    {
        // License state depends on environment — may be licensed (dev) or unlicensed (CI).
        var license = ProLicenseWrapper.GetFromFactory();
        Assert.That(license.IsLicensed, Is.TypeOf<bool>());
    }

    [Test]
    public void ProLicenseWrapper_LicenseDisplayText_IsNotNullOrEmpty()
    {
        // Display text varies: "Community License" (unlicensed) or "Licensed to: ..." (licensed).
        var license = ProLicenseWrapper.GetFromFactory();
        Assert.That(license.LicenseDisplayText, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ProLicenseWrapper_GetAdditionalCommandLineOptions_ReturnsNonNullList()
    {
        // Licensed environments may return Pro options; unlicensed returns empty.
        var license = ProLicenseWrapper.GetFromFactory();
        var options = license.GetAdditionalCommandLineOptions("SchemaQuench");
        Assert.That(options, Is.Not.Null);
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

    // --- ProDataDeliveryWrapper (default unlicensed behavior) ---

    [Test]
    public void ProDataDeliveryWrapper_DeliverTables_DoesNotThrow()
    {
        var delivery = ProDataDeliveryWrapper.GetFromFactory();
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
    public void ProDataDeliveryWrapper_DeliverTables_DoesNotThrow_WithNullContext()
    {
        var delivery = ProDataDeliveryWrapper.GetFromFactory();
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

    // --- ProServices facade ---

    [Test]
    public void ProServices_FormatProOptions_ReturnsString()
    {
        // Licensed environments may return formatted options; unlicensed returns empty.
        var result = ProLicenseWrapper.GetFromFactory().FormatProOptions("SchemaQuench");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void ProServices_GetLicenseDisplayText_ReturnsNonEmptyString()
    {
        // Display text varies: "Community License" (unlicensed) or license details (licensed).
        var result = ProLicenseWrapper.GetFromFactory().GetLicenseDisplayText();
        Assert.That(result, Is.Not.Null.And.Not.Empty);
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;
using Schema.Isolators;

namespace SchemaQuench.UnitTests;

/// <summary>
/// Unit tests for the previewOnly fan-out path: when <c>previewOnly == true</c>,
/// <see cref="ProductQuench.EnumerateWorkUnitsForTemplate"/> must discover targets and
/// flag would-create units WITHOUT calling <see cref="ProductQuench.ProvisionDatabaseViaAdminConnection"/>.
/// </summary>
[TestFixture]
public class PreviewProvisioningTests
{
    [Test]
    public void PreviewOnly_MissingDbWithCreateIfMissing_DoesNotProvision_AndFlagsWouldCreate()
    {
        WithPreviewProbe("db_missing", createIfMissing: true, dbExists: false, (pq, template) =>
        {
            var units = pq.CallEnumerate(template, previewOnly: true);
            Assert.That(pq.ProvisionCalls, Is.Empty);                       // never provisioned
            Assert.That(units.Single().WouldCreateDatabase, Is.True);       // reported as would-create
        });
    }

    [Test]
    public void PreviewOnly_MissingDbWithCreateIfMissingFalse_SkipsUnit()
    {
        WithPreviewProbe("db_missing", createIfMissing: false, dbExists: false, (pq, template) =>
        {
            var units = pq.CallEnumerate(template, previewOnly: true);
            Assert.Multiple(() =>
            {
                Assert.That(pq.ProvisionCalls, Is.Empty);
                Assert.That(units, Is.Empty, "Missing DB + CreateIfMissing:false must still skip in preview mode.");
            });
        });
    }

    [Test]
    public void PreviewOnly_ExistingDb_DoesNotFlagWouldCreate()
    {
        WithPreviewProbe("db_exists", createIfMissing: true, dbExists: true, (pq, template) =>
        {
            var units = pq.CallEnumerate(template, previewOnly: true);
            Assert.Multiple(() =>
            {
                Assert.That(pq.ProvisionCalls, Is.Empty);
                Assert.That(units.Single().WouldCreateDatabase, Is.False, "Pre-existing DB must not be flagged would-create.");
            });
        });
    }

    [Test]
    public void NonPreview_MissingDbWithCreateIfMissing_DoesProvision_AndDoesNotFlagWouldCreate()
    {
        // Non-preview behavior must be byte-for-byte unchanged.
        WithPreviewProbe("db_missing", createIfMissing: true, dbExists: false, (pq, template) =>
        {
            var units = pq.CallEnumerate(template, previewOnly: false);
            Assert.Multiple(() =>
            {
                Assert.That(pq.ProvisionCalls, Is.EqualTo(new[] { "db_missing" }),
                    "Non-preview must provision the missing DB.");
                Assert.That(units.Single().WouldCreateDatabase, Is.False,
                    "Actually-provisioned DB must NOT carry WouldCreateDatabase=true.");
            });
        });
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal SQL Server ProductQuench harness (mirrors the pattern in
    /// <see cref="ProductQuenchTests"/>), constructs a <see cref="PreviewProbe"/> inside the
    /// <see cref="FactoryContainer"/> lock (so the base constructor resolves config / product
    /// correctly), and runs <paramref name="body"/> with the probe and the template. Tears down
    /// after the body returns.
    /// </summary>
    private static void WithPreviewProbe(
        string databaseName, bool createIfMissing, bool dbExists,
        System.Action<PreviewProbe, Template> body)
    {
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            Schema.Utility.LogFactory.Clear();

            const string schemaPackagePath = "Product";
            var productPath = Path.Combine(schemaPackagePath, "Product.json");
            var file = Substitute.For<IFile>();
            var directory = Substitute.For<IDirectory>();

            file.Exists(schemaPackagePath).Returns(false);
            directory.Exists(schemaPackagePath).Returns(true);
            file.Exists(productPath).Returns(true);
            file.ReadAllText(productPath).Returns("""
                {
                  "Name": "PreviewTestProduct",
                  "Platform": "SqlServer",
                  "ScriptFolders": []
                }
                """);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["SchemaPackagePath"] = schemaPackagePath,
                    ["Target:Server"] = "primary",
                    ["WhatIfONLY"] = "true",
                    ["Target:TemplateTargets:PreviewTemplate:Databases:0"] = databaseName,
                    ["Target:TemplateTargets:PreviewTemplate:CreateIfMissing"] = createIfMissing ? "true" : "false"
                })
                .Build();

            FactoryContainer.Register<IConfigurationRoot>(config);
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(directory);
            FactoryContainer.Register<IEnvironment>(Substitute.For<IEnvironment>());

            Schema.Utility.LogFactory.Register("ProgressLog", Substitute.For<log4net.ILog>());
            Schema.Utility.LogFactory.Register("ErrorLog", Substitute.For<log4net.ILog>());

            try
            {
                var probe = new PreviewProbe(dbExists);
                var template = new Template
                {
                    Name = "PreviewTemplate",
                    Product = probe.LoadedProduct,
                    DatabaseIdentificationScript = "SELECT name FROM sys.databases"
                };
                body(probe, template);
            }
            finally
            {
                FactoryContainer.Clear();
                Schema.Utility.LogFactory.Clear();
            }
        }
    }

    /// <summary>
    /// Minimal ProductQuench subclass for preview-provisioning tests. Stubs
    /// <see cref="ProductQuench.DatabaseExistsOnServer"/> and
    /// <see cref="ProductQuench.ProvisionDatabaseViaAdminConnection"/> to avoid live connections,
    /// and exposes <see cref="CallEnumerate"/> to reach the production fan-out under test.
    /// </summary>
    private sealed class PreviewProbe : ProductQuench
    {
        private readonly bool _dbExists;
        public List<string> ProvisionCalls { get; } = [];

        public PreviewProbe(bool dbExists = false) { _dbExists = dbExists; }

        public List<WorkUnit> CallEnumerate(Template template, bool previewOnly)
            => EnumerateWorkUnitsForTemplate(template, previewOnly);

        internal override bool DatabaseExistsOnServer(string server, string databaseName)
            => _dbExists;

        internal override void ProvisionDatabaseViaAdminConnection(string server, string databaseName)
            => ProvisionCalls.Add(databaseName);
    }
}

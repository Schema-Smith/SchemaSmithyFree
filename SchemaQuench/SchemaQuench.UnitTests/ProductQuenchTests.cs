// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Isolators;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class ProductQuenchTests
{
    #region Platform Dispatch - Init Database

    [Test]
    public void GetInitDatabase_SqlServer_ReturnsMaster()
    {
        Assert.That(ProductQuench.GetInitDatabase(Platform.SqlServer), Is.EqualTo("master"));
    }

    [Test]
    public void GetInitDatabase_PostgreSQL_ReturnsPostgres()
    {
        Assert.That(ProductQuench.GetInitDatabase(Platform.PostgreSQL), Is.EqualTo("postgres"));
    }

    [Test]
    public void GetInitDatabase_MySQL_ReturnsInformationSchema()
    {
        Assert.That(ProductQuench.GetInitDatabase(Platform.MySQL), Is.EqualTo("information_schema"));
    }

    [Test]
    public void GetInitDatabase_InvalidPlatform_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductQuench.GetInitDatabase((Platform)999));
    }

    #endregion

    #region Platform Dispatch - Server ID Query

    [Test]
    public void GetServerIdQuery_SqlServer_ReturnsServerName()
    {
        Assert.That(ProductQuench.GetServerIdQuery(Platform.SqlServer), Is.EqualTo("SELECT @@SERVERNAME"));
    }

    [Test]
    public void GetServerIdQuery_PostgreSQL_ReturnsInetServerAddr()
    {
        Assert.That(ProductQuench.GetServerIdQuery(Platform.PostgreSQL), Is.EqualTo("SELECT inet_server_addr();"));
    }

    [Test]
    public void GetServerIdQuery_MySQL_ReturnsHostname()
    {
        Assert.That(ProductQuench.GetServerIdQuery(Platform.MySQL), Is.EqualTo("SELECT @@hostname"));
    }

    [Test]
    public void GetServerIdQuery_InvalidPlatform_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductQuench.GetServerIdQuery((Platform)999));
    }

    #endregion

    #region SpecialTokenTags Tests

    [Test]
    public void SpecialTokenTags_ContainsMaterializedViewSchema()
    {
        Assert.That(ProductQuench.SpecialTokenTags, Does.Contain("MaterializedViewSchema_"));
    }

    [Test]
    public void SpecialTokenTags_ContainsAllExpectedTags()
    {
        Assert.That(ProductQuench.SpecialTokenTags, Is.EquivalentTo(
            new[] { "TableSchema_", "ObjectScripts_", "QueryTokens_", "MaterializedViewSchema_", "IndexedViewSchema_" }));
    }

    [Test]
    public void SpecialTokenTags_ContainsIndexedViewSchema()
    {
        Assert.That(ProductQuench.SpecialTokenTags, Does.Contain("IndexedViewSchema_"));
    }

    #endregion

    #region Product Script Server Routing Tests

    [Test]
    public void QuenchScriptsToServerWithCheckpoint_UsesRequestedServerForCommand()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            var schemaPackagePath = "Product";
            var productPath = Path.Combine(schemaPackagePath, "Product.json");
            var file = Substitute.For<IFile>();
            var directory = Substitute.For<IDirectory>();

            file.Exists(schemaPackagePath).Returns(false);
            directory.Exists(schemaPackagePath).Returns(true);
            file.Exists(productPath).Returns(true);
            file.ReadAllText(productPath).Returns("""
                                                  {
                                                    "Name": "TestProduct",
                                                    "Platform": "SqlServer",
                                                    "ScriptFolders": []
                                                  }
                                                  """);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["SchemaPackagePath"] = schemaPackagePath,
                    ["Target:Server"] = "primary-server",
                    ["Target:SecondaryServers"] = "secondary-server",
                    ["WhatIfONLY"] = "true"
                })
                .Build();

            FactoryContainer.Register<IConfigurationRoot>(config);
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(directory);

            try
            {
                var quench = new RecordingProductQuench();

                InvokeQuenchScriptsToServerWithCheckpoint(quench, "secondary-server");

                Assert.That(quench.CommandServers, Is.EqualTo(new[] { "secondary-server" }));
            }
            finally
            {
                FactoryContainer.Clear();
            }
        }
    }

    private static void InvokeQuenchScriptsToServerWithCheckpoint(ProductQuench quench, string server)
    {
        var method = typeof(ProductQuench).GetMethod("QuenchScriptsToServerWithCheckpoint",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method!.Invoke(quench, [server, "Before Product", Array.Empty<SqlScript>(), true]);
    }

    private sealed class RecordingProductQuench : ProductQuench
    {
        public List<string> CommandServers { get; } = [];

        internal override IDbCommand GetCommand(string server)
        {
            CommandServers.Add(server);
            var command = Substitute.For<IDbCommand>();
            command.Connection.Returns(Substitute.For<IDbConnection>());
            return command;
        }
    }

    #endregion

    #region BuildSpecialTokens Tests

    [Test]
    public void BuildSpecialTokens_IncludesMaterializedViewSchemaToken()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens.ContainsKey("MaterializedViewSchema_Core"), Is.True);
    }

    [Test]
    public void BuildSpecialTokens_MaterializedViewSchema_DefaultsToEmptyArray()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["MaterializedViewSchema_Core"], Is.EqualTo("[]"));
    }

    [Test]
    public void BuildSpecialTokens_MaterializedViewSchema_EscapesSingleQuotes()
    {
        var template = new Template { Name = "Core", MaterializedViewSchema = "[{\"Name\":\"test's view\"}]" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["MaterializedViewSchema_Core"], Is.EqualTo("[{\"Name\":\"test''s view\"}]"));
    }

    [Test]
    public void BuildSpecialTokens_IncludesAllFiveTokenTypes()
    {
        var template = new Template { Name = "MyTemplate" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens.Keys, Does.Contain("TableSchema_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("ObjectScripts_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("QueryTokens_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("MaterializedViewSchema_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("IndexedViewSchema_MyTemplate"));
        Assert.That(tokens, Has.Count.EqualTo(5));
    }

    [Test]
    public void BuildSpecialTokens_IncludesIndexedViewSchemaToken()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens.ContainsKey("IndexedViewSchema_Core"), Is.True);
    }

    [Test]
    public void BuildSpecialTokens_IndexedViewSchema_DefaultsToEmptyArray()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["IndexedViewSchema_Core"], Is.EqualTo("[]"));
    }

    [Test]
    public void BuildSpecialTokens_IndexedViewSchema_EscapesSingleQuotes()
    {
        var template = new Template { Name = "Core", IndexedViewSchema = "[{\"Name\":\"test's view\"}]" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["IndexedViewSchema_Core"], Is.EqualTo("[{\"Name\":\"test''s view\"}]"));
    }

    // I8: cross-template snapshots of schema-template content (TableSchema_/
    // MaterializedViewSchema_/IndexedViewSchema_) must NOT contain the literal
    // {{SchemaName}} token — once embedded in a regular template's script, the
    // token would never be substituted at runtime and would corrupt DDL.
    [Test]
    public void BuildSpecialTokens_SchemaTemplate_TableSchema_ReplacesSchemaNameToken()
    {
        var template = new Template
        {
            Name = "TenantBody",
            SchemaIdentificationScript = "SELECT 'tenant_a'",
            TableSchema = "[{\"Schema\":\"{{SchemaName}}\",\"Name\":\"Customers\"}]"
        };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["TableSchema_TenantBody"], Does.Not.Contain("{{SchemaName}}"));
        Assert.That(tokens["TableSchema_TenantBody"], Does.Contain("<per-iteration>"));
    }

    [Test]
    public void BuildSpecialTokens_SchemaTemplate_MaterializedViewSchema_ReplacesSchemaNameToken()
    {
        var template = new Template
        {
            Name = "TenantBody",
            SchemaIdentificationScript = "SELECT 'tenant_a'",
            MaterializedViewSchema = "[{\"Schema\":\"{{SchemaName}}\",\"Name\":\"OrderSummary\"}]"
        };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["MaterializedViewSchema_TenantBody"], Does.Not.Contain("{{SchemaName}}"));
        Assert.That(tokens["MaterializedViewSchema_TenantBody"], Does.Contain("<per-iteration>"));
    }

    [Test]
    public void BuildSpecialTokens_SchemaTemplate_IndexedViewSchema_ReplacesSchemaNameToken()
    {
        var template = new Template
        {
            Name = "TenantBody",
            SchemaIdentificationScript = "SELECT 'tenant_a'",
            IndexedViewSchema = "[{\"Schema\":\"{{SchemaName}}\",\"Name\":\"vw_Orders\"}]"
        };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["IndexedViewSchema_TenantBody"], Does.Not.Contain("{{SchemaName}}"));
        Assert.That(tokens["IndexedViewSchema_TenantBody"], Does.Contain("<per-iteration>"));
    }

    [Test]
    public void BuildSpecialTokens_RegularTemplate_LeavesContentUnchanged()
    {
        // No SchemaIdentificationScript → not a schema template, so {{SchemaName}} is not
        // expected to appear; but defensively confirm the path doesn't munge regular content.
        var template = new Template
        {
            Name = "Core",
            TableSchema = "[{\"Schema\":\"dbo\",\"Name\":\"Customers\"}]"
        };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["TableSchema_Core"], Is.EqualTo("[{\"Schema\":\"dbo\",\"Name\":\"Customers\"}]"));
    }

    #endregion

    #region ReadFilterArray (Slice 5)

    [Test]
    public void ReadFilterArray_NullSection_ReturnsEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>())
            .Build();

        Assert.That(ProductQuench.ReadFilterArray(config, "Target:Templates"), Is.Empty);
    }

    [Test]
    public void ReadFilterArray_WhitespaceOnlyValues_AreFilteredOut()
    {
        // Stale config slots from prior tests come back as null or "" via the in-memory provider.
        // Whitespace-only values are equivalent — none should reach the filter.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Target:Templates:0"] = "",
                ["Target:Templates:1"] = "   ",
                ["Target:Templates:2"] = null
            })
            .Build();

        Assert.That(ProductQuench.ReadFilterArray(config, "Target:Templates"), Is.Empty);
    }

    [Test]
    public void ReadFilterArray_TrimsLeadingAndTrailingWhitespace()
    {
        // A user-supplied `" tenant_acme "` in their settings file must normalize to `"tenant_acme"`
        // so it matches the discovered universe (which never carries surrounding whitespace).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Target:Schemas:0"] = " tenant_acme",
                ["Target:Schemas:1"] = "tenant_globex ",
                ["Target:Schemas:2"] = "  tenant_beta  "
            })
            .Build();

        Assert.That(ProductQuench.ReadFilterArray(config, "Target:Schemas"),
            Is.EqualTo(new[] { "tenant_acme", "tenant_globex", "tenant_beta" }));
    }

    #endregion

    #region Schema-Template Work-Unit Enumeration (Slice 3)

    [Test]
    public void EnumerateWorkUnits_RegularTemplate_OnePerDatabase()
    {
        // Regular templates produce one work unit per (server, db) pair with empty SchemaName.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppA", "AppB", "AppC" };

            var template = new Template
            {
                Name = "Core",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases"
            };
            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Has.Count.EqualTo(3));
                Assert.That(units.Select(u => u.DatabaseName), Is.EquivalentTo(new[] { "AppA", "AppB", "AppC" }));
                Assert.That(units.All(u => u.SchemaName == ""), Is.True);
                Assert.That(units.All(u => u.Server == "primary"), Is.True);
                Assert.That(units.All(u => u.TemplateName == "Core"), Is.True);
            });
        });
    }

    [Test]
    public void EnumerateWorkUnits_SchemaTemplate_OnePerSchemaPerDb()
    {
        // Schema templates: each (server, db) hits SchemaDiscovery and produces one work unit per
        // discovered schema. The plan's worked example: 2 DBs × 4 schemas = 8 units.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "Tenants_East", "Tenants_West" };
            quench.SchemaDiscoveryResults[("primary", "Tenants_East")] =
                new List<string> { "tenant_acme", "tenant_globex", "tenant_initech", "tenant_umbrella" };
            quench.SchemaDiscoveryResults[("primary", "Tenants_West")] =
                new List<string> { "tenant_acme", "tenant_globex", "tenant_initech", "tenant_umbrella" };

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas"
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Has.Count.EqualTo(8));
                Assert.That(units.Where(u => u.DatabaseName == "Tenants_East").Select(u => u.SchemaName),
                    Is.EquivalentTo(new[] { "tenant_acme", "tenant_globex", "tenant_initech", "tenant_umbrella" }));
                Assert.That(units.All(u => u.TemplateName == "TenantBody"), Is.True);
            });
        });
    }

    [Test]
    public void EnumerateWorkUnits_RegularThreeDatabases_ThreeWorkUnits()
    {
        // §3.4 plan case: Regular template + 3 DBs → 3 work units.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppA", "AppB", "AppC" };

            var template = new Template
            {
                Name = "Core",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases"
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.That(units, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void EnumerateWorkUnits_SchemaTemplate_DifferentSchemasPerDb()
    {
        // Schema sets need not be uniform across DBs — the discovery query may return per-DB sets.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppA", "AppB" };
            quench.SchemaDiscoveryResults[("primary", "AppA")] = new List<string> { "tenant_x", "tenant_y" };
            quench.SchemaDiscoveryResults[("primary", "AppB")] = new List<string> { "tenant_z" };

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas"
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Has.Count.EqualTo(3));
                Assert.That(units.Single(u => u.DatabaseName == "AppB").SchemaName, Is.EqualTo("tenant_z"));
            });
        });
    }

    [Test]
    public void EnumerateWorkUnits_SchemaTemplate_NoSchemasReturned_NoUnitsForThatDb()
    {
        // A DB whose discovery script returns zero schemas contributes zero work units. Slice 3 with
        // a Required template + 0 schemas → empty work-unit list → required-empty error.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppEmpty" };
            quench.SchemaDiscoveryResults[("primary", "AppEmpty")] = new List<string>();

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas"
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.That(units, Is.Empty);
        });
    }

    [Test]
    public void QuenchTemplate_RequiredEmpty_RegularTemplate_SetsUpdateFailed()
    {
        // Required + zero work units → error logged + _updateFailed set. Regular-template flavor:
        // zero DBs returned by DatabaseIdentificationScript.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = System.Array.Empty<string>();

            var template = new Template
            {
                Name = "Core",
                RequireAtLeastOneTarget = true,
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases"
            };

            quench.InvokeQuenchTemplate(template);

            Assert.That(quench.UpdateFailed, Is.True);
            Assert.That(quench.LogBackupCalled, Is.True);
        });
    }

    [Test]
    public void QuenchTemplate_RequiredEmpty_SchemaTemplate_ZeroSchemas_SetsUpdateFailed()
    {
        // Required + schema template + DBs present but zero schemas discovered → still empty work
        // list → required-empty error fires. Mirrors the design's "empty across all servers" rule.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppA" };
            quench.SchemaDiscoveryResults[("primary", "AppA")] = new List<string>();

            var template = new Template
            {
                Name = "TenantBody",
                RequireAtLeastOneTarget = true,
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas"
            };

            quench.InvokeQuenchTemplate(template);

            Assert.That(quench.UpdateFailed, Is.True);
        });
    }

    [Test]
    public void QuenchTemplate_OptionalEmpty_DoesNotFail()
    {
        // Optional templates with zero work units skip silently — no error, no _updateFailed.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = System.Array.Empty<string>();

            var template = new Template
            {
                Name = "Optional",
                RequireAtLeastOneTarget = false,
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases"
            };

            quench.InvokeQuenchTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(quench.UpdateFailed, Is.False);
                Assert.That(quench.LogBackupCalled, Is.False);
            });
        });
    }

    [Test]
    public void LogSchemaTemplateFields_SchemaTemplate_EchoesFields()
    {
        // Per §3.6, templates with SchemaIdentificationScript echo their schema-fan-out config to
        // the progress log at template-start. Regular templates skip the echo. All four echoed
        // fields (SchemaIdentificationScript, CreateSchemaIfMissing, AllowParallel,
        // ContinueOnSchemaFailure) are now honored by the slice-4 engine; no stub annotation suffix.
        WithMinimalSqlServerProductQuench(quench =>
        {
            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT 1",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas",
                CreateSchemaIfMissing = true,
                AllowParallel = false,
                ContinueOnSchemaFailure = false
            };

            quench.InvokeLogSchemaTemplateFieldsIfSet(template);

            Assert.Multiple(() =>
            {
                Assert.That(quench.ProgressLogLines, Has.Some.Contains("SchemaIdentificationScript:"));
                Assert.That(quench.ProgressLogLines, Has.Some.Contains("CreateSchemaIfMissing: True"));
                Assert.That(quench.ProgressLogLines, Has.None.Contains("not yet honored"));
                Assert.That(quench.ProgressLogLines, Has.Some.Contains("AllowParallel: False"));
                Assert.That(quench.ProgressLogLines, Has.Some.Contains("ContinueOnSchemaFailure: False"));
            });
        });
    }

    [Test]
    public void LogSchemaTemplateFields_RegularTemplate_AllDefaults_LogsNothing()
    {
        // Regular template + ContinueOnDatabaseFailure at default → no echo at all.
        WithMinimalSqlServerProductQuench(quench =>
        {
            var template = new Template
            {
                Name = "Core",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT 1",
                ContinueOnDatabaseFailure = true
            };

            quench.InvokeLogSchemaTemplateFieldsIfSet(template);

            Assert.That(quench.ProgressLogLines, Is.Empty);
        });
    }

    [Test]
    public void LogSchemaTemplateFields_ContinueOnDatabaseFailureFalse_EchoesIt()
    {
        // ContinueOnDatabaseFailure applies to ALL templates (regular + schema); echoed only when
        // non-default (false). Slice 4 now wires it to actual failure routing — no stub suffix.
        WithMinimalSqlServerProductQuench(quench =>
        {
            var template = new Template
            {
                Name = "Core",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT 1",
                ContinueOnDatabaseFailure = false
            };

            quench.InvokeLogSchemaTemplateFieldsIfSet(template);

            Assert.Multiple(() =>
            {
                Assert.That(quench.ProgressLogLines, Has.Some.Contains("ContinueOnDatabaseFailure: False"));
                Assert.That(quench.ProgressLogLines, Has.None.Contains("not yet honored"));
            });
        });
    }

    [Test]
    public void LogSchemaTemplateFields_AllowParallel_NoStubAnnotation()
    {
        // AllowParallel IS consulted by slice-3 dispatch (single-thread vs MaxThreads-bounded pool).
        // It must NOT carry the "(not yet honored — slice 4)" suffix.
        WithMinimalSqlServerProductQuench(quench =>
        {
            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT 1",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas",
                AllowParallel = true
            };

            quench.InvokeLogSchemaTemplateFieldsIfSet(template);

            var allowParallelLines = quench.ProgressLogLines
                .Where(line => line.Contains("AllowParallel:"))
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(allowParallelLines, Is.Not.Empty);
                Assert.That(allowParallelLines, Has.None.Contains("not yet honored"));
            });
        });
    }

    [Test]
    public void LogSchemaTemplateFields_SchemaIdentificationScript_NoStubAnnotation()
    {
        // SchemaIdentificationScript IS load-bearing in slice 3 (schema discovery runs against it).
        // It must NOT carry the "(not yet honored — slice 4)" suffix.
        WithMinimalSqlServerProductQuench(quench =>
        {
            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT 1",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas"
            };

            quench.InvokeLogSchemaTemplateFieldsIfSet(template);

            var schemaIdLines = quench.ProgressLogLines
                .Where(line => line.Contains("SchemaIdentificationScript:"))
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(schemaIdLines, Is.Not.Empty);
                Assert.That(schemaIdLines, Has.None.Contains("not yet honored"));
            });
        });
    }

    [Test]
    public void DispatchWorkUnits_TemplateOrderPreserved_AllUnitsCompleteBeforeNextTemplate()
    {
        // Each template is dispatched via its own WorkUnitDispatcher.Run() — a synchronous, blocking
        // call. As long as QuenchTemplate iterates templates in TemplateOrder sequence (today's
        // behavior), template N+1's dispatch can't start until template N's dispatcher returns. This
        // test verifies that invariant at the dispatch layer: a single dispatcher's Run() completes
        // all its work before returning.
        var sequenceLog = new System.Collections.Concurrent.ConcurrentQueue<(string Template, string Phase)>();
        var allUnits = new List<WorkUnit>
        {
            new("s", "db1", "Shared", ""),
            new("s", "db2", "Shared", ""),
            new("s", "db3", "Shared", "")
        };

        var dispatcher = new WorkUnitDispatcher(allUnits, maxThreads: 3, new Dictionary<string, bool>(),
            unit =>
            {
                sequenceLog.Enqueue((unit.TemplateName, "start"));
                System.Threading.Thread.Sleep(20);
                sequenceLog.Enqueue((unit.TemplateName, "end"));
            });
        dispatcher.Run();

        // After Run() returns, every unit's end must have been observed. Subsequent template's
        // Run() can only start after this point — that's the TemplateOrder guarantee.
        var ends = sequenceLog.Count(e => e.Phase == "end");
        Assert.That(ends, Is.EqualTo(3), "All 3 units must have completed before Run() returns.");
    }

    [Test]
    public void EnumerateWorkUnits_ReservedSchemaName_ContinueMode_SkipsBadDbAndContinues()
    {
        // SchemaDiscovery throws on reserved names ('dbo', 'public', etc. — design §5.4).
        // With ContinueOnDatabaseFailure=true (default), a per-DB discovery failure trips
        // _updateFailed but does NOT abort enumeration — the loop continues to the next DB.
        WithMinimalSqlServerProductQuench(quench =>
        {
            // AppBad fails first; with continue=true, AppGood is still enumerated.
            quench.IdentifiedDatabases["primary"] = new[] { "AppBad", "AppGood" };
            quench.SchemaDiscoveryFailures[("primary", "AppBad")] =
                new System.InvalidOperationException("reserved schema name 'dbo'");
            quench.SchemaDiscoveryResults[("primary", "AppGood")] = new List<string> { "tenant_x" };

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas",
                ContinueOnDatabaseFailure = true  // default; explicit for clarity
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Has.Count.EqualTo(1),
                    "AppBad skipped; AppGood's unit still enumerated in continue mode.");
                Assert.That(units.Single().DatabaseName, Is.EqualTo("AppGood"));
                Assert.That(quench.UpdateFailed, Is.True, "_updateFailed set even in continue mode.");
            });
        });
    }

    [Test]
    public void EnumerateWorkUnits_ReservedSchemaName_AbortMode_StopsEnumeration()
    {
        // With ContinueOnSchemaFailure=false on a schema template, a per-DB discovery failure
        // aborts enumeration for the rest of this server — AppGood is never touched. (The
        // per-template-scope contract: schema-template failures, including discovery, honor
        // ContinueOnSchemaFailure, not ContinueOnDatabaseFailure.)
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppBad", "AppGood" };
            quench.SchemaDiscoveryFailures[("primary", "AppBad")] =
                new System.InvalidOperationException("reserved schema name 'dbo'");
            quench.SchemaDiscoveryResults[("primary", "AppGood")] = new List<string> { "tenant_x" };

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas",
                ContinueOnSchemaFailure = false
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Is.Empty,
                    "AppBad's discovery failure aborts enumeration in abort mode; AppGood is never enumerated.");
                Assert.That(quench.UpdateFailed, Is.True);
            });
        });
    }

    [Test]
    public void EnumerateWorkUnits_SchemaTemplate_PrimaryDiscoveryFailureInAbortMode_StopsBeforeSecondaryServer()
    {
        // Regression guard for #247. With ServerToQuench=Both and ContinueOnSchemaFailure=false
        // on a schema template, a primary-server schema-discovery failure must abort enumeration
        // BEFORE secondary's databases are touched. Pre-fix the outer server loop checked
        // !template.ContinueOnDatabaseFailure (true by default), so primary's abort-mode failure
        // did NOT break the outer loop and secondary's work units snuck into the dispatched list.
        // The fix swaps the predicate to ShouldAbortOnFailure(template) so the outer loop honors
        // the type-aware abort scope (ContinueOnSchemaFailure for schema templates).
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppA" };
            quench.IdentifiedDatabases["secondary"] = new[] { "AppA" };
            quench.SchemaDiscoveryFailures[("primary", "AppA")] =
                new System.InvalidOperationException("reserved schema name 'dbo'");
            // Secondary's discovery would succeed if reached — proves the fix is enumeration-level,
            // not "secondary just happened to have nothing to enumerate."
            quench.SchemaDiscoveryResults[("secondary", "AppA")] = new List<string> { "tenant_x" };

            var template = new SqlServerTemplate
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas",
                ContinueOnSchemaFailure = false,
                ServerToQuench = ServerToQuench.Both
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Is.Empty,
                    "Primary's discovery failure must abort enumeration before secondary is touched. " +
                    "Pre-#247-fix this returned 1 unit (secondary.AppA.tenant_x) and let the dispatcher " +
                    "run schema-template work on secondary despite ContinueOnSchemaFailure=false.");
                Assert.That(quench.UpdateFailed, Is.True);
            });
        }, secondaryServers: "secondary");
    }

    [Test]
    public void EnumerateWorkUnits_ReservedSchemaName_LaterDb_PreservesEarlierUnits()
    {
        // With ContinueOnDatabaseFailure=true (default): AppGood's unit is produced; AppBad's
        // failure is skipped via continue; no more DBs. The earlier unit is preserved and
        // _updateFailed is set. With continue=true the unit IS dispatched (vs slice-3 where
        // _updateFailed caused the dispatcher to skip everything — that was an inconsistency
        // the slice-4 routing resolves).
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppGood", "AppBad" };
            quench.SchemaDiscoveryResults[("primary", "AppGood")] = new List<string> { "tenant_x" };
            quench.SchemaDiscoveryFailures[("primary", "AppBad")] =
                new System.InvalidOperationException("reserved schema name 'dbo'");

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas",
                ContinueOnDatabaseFailure = true  // default; AppBad's failure is skipped
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Has.Count.EqualTo(1),
                    "AppGood's unit is preserved; AppBad's failure is skipped in continue mode.");
                Assert.That(units.Single().DatabaseName, Is.EqualTo("AppGood"));
                Assert.That(quench.UpdateFailed, Is.True, "_updateFailed set even in continue mode.");
            });
        });
    }

    /// <summary>
    /// Regression guard for the dispatch-guard bug: when ContinueOnDatabaseFailure=true and one DB's
    /// schema discovery fails, <c>_updateFailed</c> is set during enumeration but valid work units
    /// from the surviving DB are still added to the list. Before the fix the dispatch condition was
    /// <c>workUnits.Count > 0 &amp;&amp; !_updateFailed</c>, which evaluated false and silently
    /// dropped the valid units. The fix drops <c>&amp;&amp; !_updateFailed</c> so the dispatcher
    /// runs whenever there are units regardless of enumeration-time failures. This test MUST FAIL
    /// against the pre-fix code (no units dispatched) and pass after the fix (one unit dispatched).
    /// </summary>
    [Test]
    public void QuenchTemplate_ContinueOnDbFailure_ValidUnitsDispatchedDespiteEnumerationFailure()
    {
        // Two DBs: AppBad's schema discovery fails; AppGood produces one valid unit.
        // With ContinueOnDatabaseFailure=true, _updateFailed is set during enumeration but
        // AppGood's work unit is still in the list. The dispatcher must still run.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.IdentifiedDatabases["primary"] = new[] { "AppBad", "AppGood" };
            quench.SchemaDiscoveryFailures[("primary", "AppBad")] =
                new System.InvalidOperationException("reserved schema name 'dbo'");
            quench.SchemaDiscoveryResults[("primary", "AppGood")] = new List<string> { "tenant_x" };

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas",
                ContinueOnDatabaseFailure = true,
                ContinueOnSchemaFailure = true
            };

            quench.InvokeQuenchTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(quench.DispatchedWorkUnits, Has.Count.EqualTo(1),
                    "AppGood's valid unit must reach the dispatcher even though _updateFailed was set " +
                    "during AppBad's enumeration failure. Pre-fix this was 0.");
                Assert.That(quench.DispatchedWorkUnits.Single().DatabaseName, Is.EqualTo("AppGood"));
                Assert.That(quench.UpdateFailed, Is.True,
                    "_updateFailed stays set so the non-zero exit path fires after dispatch.");
            });
        });
    }

    [Test]
    public void EnumerateWorkUnits_ServerConnectionFailure_AbortsAndFlagsUpdateFailed()
    {
        // Server-level enumeration failure (unreachable server, bad DatabaseIdentificationScript)
        // must be caught, logged, and surface as _updateFailed=true. With only one server in the
        // list and no work units produced, the result is empty regardless of ContinueOnDatabaseFailure.
        WithMinimalSqlServerProductQuench(quench =>
        {
            quench.FailGetCommandFor.Add("primary");

            var template = new Template
            {
                Name = "TenantBody",
                Product = quench.LoadedProduct,
                DatabaseIdentificationScript = "SELECT name FROM sys.databases",
                SchemaIdentificationScript = "SELECT schema_name FROM sys.schemas"
            };

            var units = quench.EnumerateWorkUnitsForTemplate(template);

            Assert.Multiple(() =>
            {
                Assert.That(units, Is.Empty, "Server enumeration failed; no work units produced.");
                Assert.That(quench.UpdateFailed, Is.True);
                Assert.That(quench.ProgressLogLines,
                    Has.Some.Contains("Database enumeration FAILED"),
                    "Server-level failure must be logged so ops can see it without trawling the error log.");
            });
        });
    }

    /// <summary>
    /// Hand-builds a minimal SQL Server ProductQuench (no real connections, no Product.json on disk)
    /// inside the FactoryContainer lock and runs <paramref name="body"/> against the recorded test
    /// double. Keeps every schema-template work-unit test self-contained: build → assert → tear down.
    /// </summary>
    private static void WithMinimalSqlServerProductQuench(
        System.Action<RecordingWorkUnitProductQuench> body,
        string secondaryServers = null)
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
                                                    "Name": "TestProduct",
                                                    "Platform": "SqlServer",
                                                    "ScriptFolders": []
                                                  }
                                                  """);

            var configValues = new Dictionary<string, string>
            {
                ["SchemaPackagePath"] = schemaPackagePath,
                ["Target:Server"] = "primary",
                ["WhatIfONLY"] = "true"
            };
            if (!string.IsNullOrEmpty(secondaryServers))
                configValues["Target:SecondaryServers"] = secondaryServers;
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            FactoryContainer.Register<IConfigurationRoot>(config);
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(directory);

            // CRITICAL: prevent LogBackup.BackupLogsAndExit's Environment.Exit(n) from killing the
            // test host. The QuenchTemplate failure paths route through BackupLogsAndExit, which
            // calls Environment.Exit unless IEnvironment is mocked. Without this substitute the
            // first required-empty test brings down the whole nunit host.
            FactoryContainer.Register<IEnvironment>(Substitute.For<IEnvironment>());

            // Substitute a capture-friendly logger BEFORE constructing ProductQuench so its
            // _progressLog field (captured in the constructor) points at the fake.
            var progressLog = Substitute.For<log4net.ILog>();
            var progressLogLines = new List<string>();
            progressLog.When(l => l.Info(Arg.Any<object>()))
                .Do(ci => progressLogLines.Add($"{ci.Arg<object>()}"));
            progressLog.When(l => l.Error(Arg.Any<object>()))
                .Do(ci => progressLogLines.Add($"ERROR: {ci.Arg<object>()}"));
            progressLog.When(l => l.Warn(Arg.Any<object>()))
                .Do(ci => progressLogLines.Add($"WARN: {ci.Arg<object>()}"));
            Schema.Utility.LogFactory.Register("ProgressLog", progressLog);
            Schema.Utility.LogFactory.Register("ErrorLog", Substitute.For<log4net.ILog>());

            try
            {
                var quench = new RecordingWorkUnitProductQuench(progressLogLines);
                body(quench);
            }
            finally
            {
                FactoryContainer.Clear();
                Schema.Utility.LogFactory.Clear();
            }
        }
    }

    /// <summary>
    /// ProductQuench test double for slice-3 work-unit enumeration. Stubs out the live-connection
    /// surface (<see cref="ProductQuench.GetCommand"/>, <see cref="ProductQuench.DiscoverSchemas"/>)
    /// with in-memory result tables, captures the progress-log output for inspection, and exposes
    /// the private <c>_updateFailed</c> flag via reflection so tests can assert on it.
    /// </summary>
    private sealed class RecordingWorkUnitProductQuench : ProductQuench
    {
        public Dictionary<string, string[]> IdentifiedDatabases { get; } = new();
        public Dictionary<(string Server, string Db), List<string>> SchemaDiscoveryResults { get; } = new();
        public Dictionary<(string Server, string Db), System.Exception> SchemaDiscoveryFailures { get; } = new();
        public HashSet<string> FailGetCommandFor { get; } = new();
        public List<string> ProgressLogLines { get; }
        public bool LogBackupCalled { get; private set; }

        public RecordingWorkUnitProductQuench(List<string> progressLogLines)
        {
            ProgressLogLines = progressLogLines;
        }

        internal override IDbCommand GetCommand(string server)
        {
            if (FailGetCommandFor.Contains(server))
                throw new System.Exception($"Unable to connect to {server} (simulated)");
            var dbs = IdentifiedDatabases.TryGetValue(server, out var list) ? list : System.Array.Empty<string>();
            return MakeReaderCommand(dbs);
        }

        internal override List<string> DiscoverSchemas(string server, string databaseName, Template template)
        {
            if (SchemaDiscoveryFailures.TryGetValue((server, databaseName), out var ex))
                throw ex;
            return SchemaDiscoveryResults.TryGetValue((server, databaseName), out var list)
                ? new List<string>(list)
                : new List<string>();
        }

        // Capture, don't dispatch. Slice-3 enumeration tests only need to assert on the work
        // unit list shape — running the dispatcher would require a live DatabaseQuench.Execute()
        // and a real connection, neither of which is in scope at this layer.
        public List<WorkUnit> DispatchedWorkUnits { get; } = new();

        internal override void DispatchWorkUnits(Template template, List<WorkUnit> workUnits, bool suppressKindling)
        {
            DispatchedWorkUnits.AddRange(workUnits);
        }

        public bool UpdateFailed
        {
            get
            {
                var fi = typeof(ProductQuench).GetField("_updateFailed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                return (bool)fi!.GetValue(this);
            }
        }

        public void InvokeQuenchTemplate(Template template)
        {
            try
            {
                var method = typeof(ProductQuench).GetMethod("QuenchTemplate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method!.Invoke(this, new object[] { template, true });
            }
            catch (System.Reflection.TargetInvocationException tie) when (
                tie.InnerException is System.Exception { Message: var m } && m.Contains("BackupLogsAndExit"))
            {
                // LogBackup.BackupLogsAndExit calls Environment.Exit in production; in tests the
                // file-backup mock no-ops but might still throw. Either way the test observes the
                // failure through UpdateFailed.
                LogBackupCalled = true;
            }

            // The check at the bottom of QuenchTemplate calls LogBackup.BackupLogsAndExit when
            // _updateFailed is true. Under tests it's a no-op; surface the equivalent observation
            // by reading _updateFailed directly.
            LogBackupCalled = LogBackupCalled || UpdateFailed;
        }

        public void InvokeLogSchemaTemplateFieldsIfSet(Template template)
        {
            ProgressLogLines.Clear();
            var method = typeof(ProductQuench).GetMethod("LogSchemaTemplateFieldsIfSet",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method!.Invoke(this, new object[] { template });
        }

        private static IDbCommand MakeReaderCommand(string[] rows)
        {
            var command = Substitute.For<IDbCommand>();
            command.Connection.Returns(Substitute.For<IDbConnection>());
            command.ExecuteReader().Returns(_ => new InMemoryRowReader(rows));
            return command;
        }
    }

    /// <summary>
    /// Single-column <see cref="IDataReader"/> stub that yields the supplied string values in order.
    /// Mirrors the shape of what <c>SELECT name FROM sys.databases</c> would produce — one column,
    /// N rows.
    /// </summary>
    private sealed class InMemoryRowReader : IDataReader
    {
        private readonly string[] _rows;
        private int _idx = -1;

        public InMemoryRowReader(string[] rows) { _rows = rows ?? System.Array.Empty<string>(); }

        public bool Read() { _idx++; return _idx < _rows.Length; }

        public object this[int i] => _rows[_idx];
        public object this[string name] => _rows[_idx];

        public void Dispose() { }
        public void Close() { }
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => -1;
        public int FieldCount => 1;

        public string GetName(int i) => "name";
        public string GetDataTypeName(int i) => "VARCHAR";
        public System.Type GetFieldType(int i) => typeof(string);
        public object GetValue(int i) => _rows[_idx];
        public int GetValues(object[] values) { values[0] = _rows[_idx]; return 1; }
        public int GetOrdinal(string name) => 0;
        public bool GetBoolean(int i) => throw new System.NotSupportedException();
        public byte GetByte(int i) => throw new System.NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => throw new System.NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => 0;
        public System.Guid GetGuid(int i) => throw new System.NotSupportedException();
        public short GetInt16(int i) => throw new System.NotSupportedException();
        public int GetInt32(int i) => throw new System.NotSupportedException();
        public long GetInt64(int i) => throw new System.NotSupportedException();
        public float GetFloat(int i) => throw new System.NotSupportedException();
        public double GetDouble(int i) => throw new System.NotSupportedException();
        public string GetString(int i) => _rows[_idx];
        public decimal GetDecimal(int i) => throw new System.NotSupportedException();
        public System.DateTime GetDateTime(int i) => throw new System.NotSupportedException();
        public IDataReader GetData(int i) => throw new System.NotSupportedException();
        public bool IsDBNull(int i) => _rows[_idx] == null;
        public System.Data.DataTable GetSchemaTable() => null;
    }

    #endregion
}

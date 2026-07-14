// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

using NUnit.Framework;

namespace SchemaQuench.IntegrationTests.Shared;

/// <summary>
/// Folder-gate (#260) engine-facing contract against a live MySQL: MySQL returns an <c>Int64</c>
/// (0/1) scalar rather than a native boolean, so this pins that <see cref="FolderGate.ShouldApply"/>
/// (via <c>ScalarToBool</c>) interprets it correctly — the one genuinely engine-specific seam. The
/// folder-filtering / slot-rebuild logic is engine-agnostic (unit-covered).
/// </summary>
public abstract class FolderGateIntegrationTestsSharedTests
{
    protected abstract Platform Platform { get; }

    private readonly string _connectionString;

    protected FolderGateIntegrationTestsSharedTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform, config["Target:Server"], "information_schema",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
    }

    [Test]
    public void FolderGate_LiveMySql_InterpretsBooleanPredicates()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.Multiple(() =>
        {
            Assert.That(FolderGate.ShouldApply(cmd, ""), Is.True, "Blank expression always applies.");
            Assert.That(FolderGate.ShouldApply(cmd, "SELECT 1"), Is.True);
            Assert.That(FolderGate.ShouldApply(cmd, "SELECT 0"), Is.False);
            Assert.That(
                FolderGate.ShouldApply(cmd, "SELECT CASE WHEN @@version LIKE '%' THEN 1 ELSE 0 END"),
                Is.True, "A real predicate returning Int64 evaluates true on MySQL.");
        });
    }

    [Test]
    public void FolderGate_LiveMySql_EvaluatesResolvedScriptToken()
    {
        // #260 fix: a gate may reference a script token, which is resolved before evaluation.
        // Pre-fix the unresolved '{{EnvType}}' would never equal 'prod' and the gate would read false.
        var folder = new TemplateFolder
        {
            FolderPath = "EnvGated",
            QuenchSlot = TemplateQuenchSlot.Before,
            ShouldApplyExpression = "SELECT CASE WHEN '{{EnvType}}' = 'prod' THEN 1 ELSE 0 END"
        };
        folder.LoadSqlFiles(NonexistentBasePath(), [new KeyValuePair<string, string>("EnvType", "prod")], Platform);

        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.That(FolderGate.ShouldApply(cmd, folder.ShouldApplyExpression), Is.True);
    }

    private static string NonexistentBasePath() =>
        Path.Combine(Path.GetTempPath(), "ss-folder-gate-" + Guid.NewGuid().ToString("N"));
}

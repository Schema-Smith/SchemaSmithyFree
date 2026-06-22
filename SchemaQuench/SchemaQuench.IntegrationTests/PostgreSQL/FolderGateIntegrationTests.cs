// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

/// <summary>
/// Folder-gate (#260) engine-facing contract against a live PostgreSQL: PostgreSQL returns a native
/// <c>boolean</c> scalar, so this pins that <see cref="FolderGate.ShouldApply"/> interprets it
/// correctly. The folder-filtering / slot-rebuild logic itself is engine-agnostic (unit-covered).
/// </summary>
[Category("PostgreSQL")]
public class FolderGateIntegrationTests
{
    private readonly string _connectionString;

    public FolderGateIntegrationTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
    }

    [Test]
    public void FolderGate_LivePostgreSql_InterpretsBooleanPredicates()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.Multiple(() =>
        {
            Assert.That(FolderGate.ShouldApply(cmd, ""), Is.True, "Blank expression always applies.");
            Assert.That(FolderGate.ShouldApply(cmd, "SELECT true"), Is.True);
            Assert.That(FolderGate.ShouldApply(cmd, "SELECT false"), Is.False);
            Assert.That(
                FolderGate.ShouldApply(cmd, "SELECT version() ILIKE '%PostgreSQL%'"),
                Is.True, "A real native-boolean predicate evaluates true on PostgreSQL.");
        });
    }

    [Test]
    public void FolderGate_LivePostgreSql_EvaluatesResolvedScriptToken()
    {
        // #260 fix: a gate may reference a script token, which is resolved before evaluation.
        // Pre-fix the unresolved '{{EnvType}}' would never equal 'prod' and the gate would read false.
        var folder = new TemplateFolder
        {
            FolderPath = "EnvGated",
            QuenchSlot = TemplateQuenchSlot.Before,
            ShouldApplyExpression = "SELECT '{{EnvType}}' = 'prod'"
        };
        folder.LoadSqlFiles(NonexistentBasePath(), [new KeyValuePair<string, string>("EnvType", "prod")], Platform.PostgreSQL);

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.That(FolderGate.ShouldApply(cmd, folder.ShouldApplyExpression), Is.True);
    }

    private static string NonexistentBasePath() =>
        Path.Combine(Path.GetTempPath(), "ss-folder-gate-" + Guid.NewGuid().ToString("N"));
}

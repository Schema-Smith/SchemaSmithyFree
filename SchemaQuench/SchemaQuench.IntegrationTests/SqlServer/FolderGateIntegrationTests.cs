// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Folder-gate (#260) engine-facing contract against a live SQL Server: a folder's
/// <c>ShouldApplyExpression</c> is run as a scalar query and interpreted as a boolean. The
/// folder-filtering / slot-rebuild logic is engine-agnostic C# (unit-covered); what's genuinely
/// engine-specific is how each engine returns a boolean scalar, so that's what these per-engine
/// tests pin (SQL Server returns bit/int here).
/// </summary>
[Category("SqlServer")]
public class FolderGateIntegrationTests
{
    private readonly string _connectionString;

    public FolderGateIntegrationTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
    }

    [Test]
    public void FolderGate_LiveSqlServer_InterpretsBooleanPredicates()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.Multiple(() =>
        {
            Assert.That(FolderGate.ShouldApply(cmd, ""), Is.True, "Blank expression always applies.");
            Assert.That(FolderGate.ShouldApply(cmd, "SELECT 1"), Is.True);
            Assert.That(FolderGate.ShouldApply(cmd, "SELECT 0"), Is.False);
            Assert.That(
                FolderGate.ShouldApply(cmd, "SELECT CASE WHEN @@VERSION LIKE '%Microsoft%' THEN 1 ELSE 0 END"),
                Is.True, "A real server-property predicate evaluates true on SQL Server.");
        });
    }
}

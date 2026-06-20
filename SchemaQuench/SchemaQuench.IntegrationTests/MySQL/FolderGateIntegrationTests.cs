// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Folder-gate (#260) engine-facing contract against a live MySQL: MySQL returns an <c>Int64</c>
/// (0/1) scalar rather than a native boolean, so this pins that <see cref="FolderGate.ShouldApply"/>
/// (via <c>ScalarToBool</c>) interprets it correctly — the one genuinely engine-specific seam. The
/// folder-filtering / slot-rebuild logic is engine-agnostic (unit-covered).
/// </summary>
[Category("MySQL")]
public class FolderGateIntegrationTests
{
    private readonly string _connectionString;

    public FolderGateIntegrationTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.MySQL, config["Target:Server"], "information_schema",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
    }

    [Test]
    public void FolderGate_LiveMySql_InterpretsBooleanPredicates()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
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
}

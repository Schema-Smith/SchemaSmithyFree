// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.Shared;

public abstract class ServerVersionHelperIntegrationTestsSharedTests : BaseTableQuenchTests
{
    [Test]
    public void ServerVersionNum_ReturnsRealComparable_AtOrAboveFloor()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith_ServerVersionNum()";
        // Floor is per-platform: MySQL 5.7 (507), MariaDB 10.2 (1002).
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThanOrEqualTo(VersionHelper.HardFloor(Platform)));
    }

    [Test]
    public void ServerVersionNum_HonorsSessionVariableOverride()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SET @schemasmith_version_override = 804";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT SchemaSmith_ServerVersionNum()";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(804));

        cmd.CommandText = "SET @schemasmith_version_override = NULL";
        cmd.ExecuteNonQuery();
    }
}

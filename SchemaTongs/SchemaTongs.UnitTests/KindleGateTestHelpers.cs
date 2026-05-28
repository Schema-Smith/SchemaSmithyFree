// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NSubstitute;
using Schema.Domain;
using Schema.Utility;

namespace SchemaTongs.UnitTests;

internal static class KindleGateTestHelpers
{
    /// <summary>
    /// Stub an NSubstitute IDbCommand mock so a SchemaTongs unit test flowing through
    /// ForgeKindler.KindleTheForge takes the version-gate SKIP path without executing any
    /// kindle DDL on the mock. The stub returns 1L for the MySQL GET_LOCK acquire, 1L for the
    /// SchemaSmith_KindleStamp existence probe against information_schema.tables, and the current
    /// ComputeKindleStamp(MySQL) for the actual stamp SELECT (so the gate compares equal and
    /// returns). Unrelated ExecuteScalar calls fall through to null, preserving any per-test
    /// stubbing the caller has set up for its own queries.
    /// </summary>
    public static void StubMySqlKindleGate(IDbCommand command)
    {
        var stamp = ForgeKindler.ComputeKindleStamp(Platform.MySQL);
        command.ExecuteScalar().Returns(_ =>
        {
            var sql = command.CommandText ?? string.Empty;
            if (sql.Contains("GET_LOCK"))
                return (object)1L;
            if (sql.Contains("information_schema.tables"))
                return (object)1L;
            if (sql.Contains("SchemaSmith_KindleStamp") && sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return (object)stamp;
            return null;
        });
    }
}

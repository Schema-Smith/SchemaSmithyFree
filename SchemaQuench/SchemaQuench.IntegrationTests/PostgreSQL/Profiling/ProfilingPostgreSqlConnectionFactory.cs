// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;

namespace SchemaQuench.IntegrationTests.PostgreSQL.Profiling;

/// <summary>
/// IDbConnectionFactory decorator used by the PG connection-discipline investigation (Phase 1).
/// Delegates the actual connection creation to PostgreSqlConnectionFactory; wraps the result in a
/// ProfilingConnection so Open/Close/Dispose events flow through the recorder.
///
/// <para>Registered in the test fixture via <c>FactoryContainer.Register&lt;IDbConnectionFactory&gt;(...)</c>.
/// Because the schema-template tests only exercise PG paths, the interface-keyed registration
/// is safe for the duration of the run.</para>
/// </summary>
public sealed class ProfilingPostgreSqlConnectionFactory : IDbConnectionFactory
{
    private readonly IDbConnectionFactory _inner = new PostgreSqlConnectionFactory();
    private readonly ProfilingConnectionRecorder _recorder;

    public ProfilingPostgreSqlConnectionFactory(ProfilingConnectionRecorder recorder)
    {
        _recorder = recorder;
    }

    public IDbConnection GetDbConnection(string connectionString)
    {
        var inner = _inner.GetDbConnection(connectionString);
        return new ProfilingConnection(inner, _recorder);
    }
}

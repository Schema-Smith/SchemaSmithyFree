// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;

namespace Schema.DataAccess;

public interface IDbConnectionFactory
{
    IDbConnection GetDbConnection(string connectionString);
}

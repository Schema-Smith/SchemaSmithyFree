// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System.Data;

﻿using System.Data;

namespace Schema.DataAccess;

public interface ISqlConnectionFactory
{
    IDbConnection GetSqlConnection(string connectionString);
}
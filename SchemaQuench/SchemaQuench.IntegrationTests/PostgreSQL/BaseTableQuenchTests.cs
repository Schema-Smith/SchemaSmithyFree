// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

﻿using System;
using System.Data;
using System.Threading;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using SchemaSmith.Pro;
using Microsoft.Extensions.Configuration;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
public class BaseTableQuenchTests
{
    protected readonly string _connectionString;
    protected readonly string _mainDb;
    protected readonly string _productName = "Quench Table Tests";

    public BaseTableQuenchTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres", config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    protected void RunTableQuenchProc(IDbCommand cmd, string json, bool indexOnly = false)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = indexOnly
            ? @$"
CALL ""SchemaSmith"".""IndexOnlyQuench""(p_ProductName := '{_productName}', p_TableDefinitions := '{json.Replace("'", "''")}', p_DropUnknownIndexes := true);
CALL ""SchemaSmith"".""FixupIndexOwnership""(p_ProductName := '{_productName}');
"
            : $"CALL \"SchemaSmith\".\"TableQuench\"(p_ProductName := '{_productName}', p_TableDefinitions := '{json.Replace("'", "''")}', p_DropTablesRemovedFromProduct := false, p_DropUnknownIndexes := false)";
        var retry = true;
        var tries = 0;
        while (retry && tries++ < 10)
        {
            try
            {
                cmd.ExecuteNonQuery();
                retry = false;
            }
            catch (Exception e)
            {
                if (!e.Message.ContainsIgnoringCase("deadlock detected")) throw;
                Thread.Sleep(1000);
            }
        }
    }

    protected static string GetColumnDataType(IDbCommand cmd, string tableSchema, string tableName, string columnName)
    {
        cmd.CommandText = @$"
SELECT CASE WHEN c.domain_name IS NOT NULL
                  THEN CASE WHEN c.domain_schema != 'pg_catalog' THEN '""' || c.domain_schema || '"".' ELSE '' END || '""' || c.domain_name || '""'
                  ELSE CASE WHEN c.udt_schema != 'pg_catalog' THEN c.udt_schema || '.' ELSE '' END || c.udt_name
                  END ||
       CASE WHEN UPPER(c.udt_name) LIKE '%CHAR'
            THEN CASE WHEN COALESCE(c.character_maximum_length, -1) = -1 THEN '' ELSE '(' || c.character_maximum_length || ')' END
            WHEN UPPER(c.udt_name) IN ('NUMERIC', 'DECIMAL')
            THEN  '(' || c.numeric_precision || ', ' || c.numeric_scale || ')'
            WHEN UPPER(c.udt_name) = 'TIMESTAMP' AND COALESCE(c.datetime_precision, 6) != 6
            THEN  '(' || c.datetime_precision || ')'
            ELSE '' END
  FROM information_schema.columns c 
  WHERE table_schema = '{tableSchema}' 
    AND table_name = '{tableName}'
    AND column_name = '{columnName}'";
        return cmd.ExecuteScalar()?.ToString()?.ToUpper() ?? "UNKNOWN";
    }
}

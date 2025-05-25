using System.Data;
using Schema.DataAccess;
using Schema.Isolators;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench.IntegrationTests;

public class BaseTableQuenchTests
{
    protected readonly string _connectionString;
    protected readonly string _mainDb;
    protected readonly string _productName = "Quench Table Tests";

    public BaseTableQuenchTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        _connectionString = ConnectionString.Build(config["Target:Server"], "master", config["Target:User"], config["Target:Password"]);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    protected void RunTableQuenchProc(IDbCommand cmd, string json)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = $"EXEC SchemaSmith.TableQuench @ProductName = '{_productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0";
        cmd.ExecuteNonQuery();
    }

    protected static string GetColumnDataType(IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = @$"
SELECT UPPER(USER_TYPE) + CASE WHEN USER_TYPE LIKE '%CHAR' OR USER_TYPE LIKE '%BINARY'
                               THEN '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'MAX' ELSE CONVERT(VARCHAR(20), CHARACTER_MAXIMUM_LENGTH) END + ')'
                               WHEN USER_TYPE IN ('NUMERIC', 'DECIMAL')
                               THEN  '(' + CONVERT(VARCHAR(20), NUMERIC_PRECISION) + ', ' + CONVERT(VARCHAR(20), NUMERIC_SCALE) + ')'
                               WHEN USER_TYPE = 'DATETIME2'
                               THEN  '(' + CONVERT(VARCHAR(20), DATETIME_PRECISION) + ')'
                               WHEN USER_TYPE = 'XML' AND sc.xml_collection_id <> 0
                               THEN  '(' + (SELECT '[' + SCHEMA_NAME(xc.[schema_id]) + '].[' + xc.[name] + ']' FROM sys.xml_schema_collections xc WHERE xc.xml_collection_id = sc.xml_collection_id) + ')'
                               WHEN USER_TYPE = 'UNIQUEIDENTIFIER' AND sc.is_rowguidcol = 1
                               THEN  ' ROWGUIDCOL'
                               ELSE '' END +
                          CASE WHEN ident.column_id IS NOT NULL
                               THEN ' IDENTITY(' + CONVERT(VARCHAR(20), ident.seed_value) + ', ' + CONVERT(VARCHAR(20), ident.increment_value) + ')' +
                                    CASE WHEN ident.is_not_for_replication = 1 THEN ' NOT FOR REPLICATION' ELSE '' END
                               ELSE '' END
  FROM INFORMATION_SCHEMA.COLUMNS c WITH (NOLOCK)
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AND sc.[name] = c.COLUMN_NAME
  JOIN (SELECT CASE WHEN SCHEMA_NAME(st.[schema_id]) IN ('sys', 'dbo')
                    THEN '' ELSE SCHEMA_NAME(st.[schema_id]) + '.' END + st.[name] AS USER_TYPE, st.user_type_id
          FROM sys.types st WITH (NOLOCK)) st ON st.user_type_id = sc.user_type_id
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = c.COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
  WHERE TABLE_SCHEMA = 'dbo' 
    AND TABLE_NAME = '{tableName}'
    AND COLUMN_NAME = '{columnName}'";
        return cmd.ExecuteScalar()!.ToString()!.ToUpper();
    }
}

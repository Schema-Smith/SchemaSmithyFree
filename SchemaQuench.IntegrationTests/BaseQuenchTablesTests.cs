using System.Data;
using Schema.DataAccess;
using Schema.Isolators;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench.IntegrationTests;

public class BaseQuenchTablesTests
{
    protected readonly string _connectionString;
    protected readonly string _mainDb;
    protected readonly string _productName = "Quench Table Tests";

    public BaseQuenchTablesTests()
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
SELECT UPPER(DATA_TYPE) + CASE WHEN DATA_TYPE LIKE '%CHAR' OR DATA_TYPE LIKE '%BINARY'
                           THEN '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'MAX' ELSE CONVERT(VARCHAR(20), CHARACTER_MAXIMUM_LENGTH) END + ')'
                           WHEN DATA_TYPE IN ('NUMERIC', 'DECIMAL')
                           THEN  '(' + CONVERT(VARCHAR(20), NUMERIC_PRECISION) + ', ' + CONVERT(VARCHAR(20), NUMERIC_SCALE) + ')'
                           WHEN DATA_TYPE = 'DATETIME2'
                           THEN  '(' + CONVERT(VARCHAR(20), DATETIME_PRECISION) + ')'
                           ELSE '' END AS DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS c WITH (NOLOCK)
JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AND sc.[name] = c.COLUMN_NAME
JOIN (SELECT CASE WHEN SCHEMA_NAME(st.[schema_id]) IN ('sys', 'dbo')
                  THEN '' ELSE SCHEMA_NAME(st.[schema_id]) + '.' END + st.[name] AS USER_TYPE, st.user_type_id
        FROM sys.types st WITH (NOLOCK)) st ON st.user_type_id = sc.user_type_id
WHERE TABLE_SCHEMA = 'dbo' 
AND TABLE_NAME = '{tableName}'
AND COLUMN_NAME = '{columnName}'";
        return cmd.ExecuteScalar()?.ToString().ToUpper();
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Isolators;
using Schema.Utility;

namespace DataTongs;

public class DataTongs
{
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");

    private IDbConnection GetConnection(string targetDb)
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();

        var connectionString = ConnectionString.Build(config["Source:Server"], targetDb, config["Source:User"], config["Source:Password"]);

        var connection = SqlConnectionFactory.GetFromFactory().GetSqlConnection(connectionString);

        connection.Open();
        return connection;
    }

    public void CastData()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();

        var mergeUpdate = config["ShouldCast:MergeUpdate"]?.ToLower() != "false";
        var mergeDelete = config["ShouldCast:MergeDelete"]?.ToLower() != "false";
        var outputPath = Path.Combine(config["OutputPath"] ?? ".");
        DirectoryWrapper.GetFromFactory().CreateDirectory(outputPath);

        var sourceDb = config["Source:Database"]!;
        if (string.IsNullOrEmpty(sourceDb)) throw new Exception("Source database is required");

        var tables = config.GetSection("Tables")
            .AsEnumerable()
            .Where(x => x.Value != null)
            .Select(x => new KeyValuePair<string, string>(x.Key.Replace("Tables:", ""), x.Value!));
        var tableFilters = config.GetSection("TableFilters")
            .AsEnumerable()
            .Where(x => !x.Key.Equals("TableFilters"))
            .ToDictionary(t => t.Key.Replace("TableFilters:", ""), t => t.Value);

        _progressLog.Info("Starting DataTongs...");
        using var sourceConnection = GetConnection(sourceDb);
        var cmd = sourceConnection.CreateCommand();
        foreach (var table in tables)
        {
            _progressLog.Info($"  Casting data for: {table.Key}");
            var parts = table.Key.Split('.').Select(p => p.Trim()).ToArray();
            var tableSchema = parts.Length == 2 ? parts[0] : "dbo";
            var tableName = parts.Length == 2 ? parts[1] : parts[0];
            var matchColumns = string.Join(" AND ", table.Value.Split(',').Select(c => $"Source.[{c.Trim().Trim(']', '[')}] = Target.[{c.Trim().Trim(']', '[')}]"));
            var orderColumns = string.Join(",", table.Value.Split(',').Select(c => $"[{c.Trim().Trim(']', '[')}]"));

            var selectColumns = GetSelectColumns(cmd, tableSchema, tableName);
            tableFilters.TryGetValue(table.Key, out var filter);
            var tableData = GetTableData(cmd, selectColumns, tableSchema, tableName, orderColumns, filter);

            var mergeSQL = BuildMergeSql(cmd, tableSchema, tableName, tableData, matchColumns, mergeUpdate, mergeDelete, filter);

            FileWrapper.GetFromFactory().WriteAllText(Path.Combine(outputPath, $"Populate {tableSchema}.{tableName}.sql"), mergeSQL);
        }
        sourceConnection.Close();
        _progressLog.Info("DataTongs completed successfully.");
    }

    private static string BuildMergeSql(IDbCommand cmd, string tableSchema, string tableName, string? tableData, string matchColumns, bool mergeUpdate, bool mergeDelete, string? filter)
    {
        var fromJsonSelectColumns = GetFromJsonSelectColumns(cmd, tableSchema, tableName);
        var insertColumns = GetInsertColumns(cmd, tableSchema, tableName);
        var identityInsert = CheckIdentityInsertRequired(cmd, tableSchema, tableName);
        var jsonColumns = GetJsonColumns(cmd, tableSchema, tableName);

        var mergeSQL = $@"
DECLARE @v_json NVARCHAR(MAX) = '{tableData?.Replace("'", "''")}';

{(identityInsert ? $"SET IDENTITY_INSERT [{tableSchema}].[{tableName}] ON;" : "")} 
MERGE INTO [{tableSchema}].[{tableName}] AS Target
USING (
  SELECT {fromJsonSelectColumns}
    FROM OPENJSON(@v_json)
    WITH (
{jsonColumns}
    )
) AS Source
ON {matchColumns}
";
        if (mergeUpdate)
        {
            var updateColumns = GetUpdateColumns(cmd, tableSchema, tableName);
            var updateCompare = string.Join(" AND ",
                updateColumns!.Split(',').Select(c => c.StartsWith("G[")
                    ? $"NOT (Target.{c.Substring(1)}.ToString() = Source.{c.Substring(1)}.ToString() OR (Target.{c.Substring(1)} IS NULL AND Source.{c.Substring(1)} IS NULL))"
                    : c.StartsWith("X[")
                        ? $"NOT (CAST(Target.{c.Substring(1)} AS NVARCHAR(MAX)) = CAST(Source.{c.Substring(1)} AS NVARCHAR(MAX)) OR (Target.{c.Substring(1)} IS NULL AND Source.{c.Substring(1)} IS NULL))"
                        : $"NOT (Target.{c} = Source.{c} OR (Target.{c} IS NULL AND Source.{c} IS NULL))"));

            mergeSQL += $@"

WHEN MATCHED AND ({updateCompare}) THEN
  UPDATE SET
{string.Join(",\r\n", updateColumns!.Split(',').Select(c => $"        {c.Replace("G[", "[").Replace("X[", "[")} = Source.{c.Replace("G[", "[").Replace("X[", "[")}"))}
";
        }

        mergeSQL += $@"

WHEN NOT MATCHED THEN
  INSERT (
{insertColumns}
  ) VALUES (
{insertColumns!.Replace("[", "Source.[")}  
  )
";

        if (mergeDelete)
        {
            mergeSQL += $@"
 
 WHEN NOT MATCHED BY SOURCE{(string.IsNullOrWhiteSpace(filter) ? "" : $" AND ({filter})")} THEN
   DELETE 
 ";
        }

        mergeSQL += $";\r\n{(identityInsert ? $"SET IDENTITY_INSERT [{tableSchema}].[{tableName}] OFF;" : "")} \r\n";
        return mergeSQL;
    }

    private static string? GetTableData(IDbCommand cmd, string? selectColumns, string tableSchema, string tableName, string orderColumns, string? filter)
    {
        cmd.CommandText = $@"
SELECT CAST((
SELECT {selectColumns} 
  FROM [{tableSchema}].[{tableName}] WITH (NOLOCK) 
  {(string.IsNullOrWhiteSpace(filter) ? "" : $"WHERE {filter}")}
  ORDER BY {orderColumns}
  FOR JSON AUTO) AS NVARCHAR(MAX))
";
        return cmd.ExecuteScalar()?.ToString()!.Replace("},{", "},\r\n{").Replace("[{", "[\r\n{").Replace("}]", "}\r\n]");
    }

    private static string? GetJsonColumns(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT STRING_AGG('           [' + c.COLUMN_NAME + '] ' + 
       REPLACE(REPLACE(UPPER(USER_TYPE), 'HIERARCHYID', 'NVARCHAR(4000)'), 'GEOGRAPHY', 'NVARCHAR(4000)') + 
           CASE WHEN USER_TYPE LIKE '%CHAR' OR USER_TYPE LIKE '%BINARY'
                THEN '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'MAX' ELSE CONVERT(NVARCHAR(20), CHARACTER_MAXIMUM_LENGTH) END + ')'
                                WHEN USER_TYPE IN ('NUMERIC', 'DECIMAL')
                                THEN  '(' + CONVERT(NVARCHAR(20), NUMERIC_PRECISION) + ', ' + CONVERT(NVARCHAR(20), NUMERIC_SCALE) + ')'
                                WHEN USER_TYPE = 'DATETIME2'
                                THEN  '(' + CONVERT(NVARCHAR(20), DATETIME_PRECISION) + ')'
                                WHEN USER_TYPE = 'XML' AND sc.xml_collection_id <> 0
                                THEN  '([' + SCHEMA_NAME(xc.[schema_id]) + '].[' + xc.[name] + '])'
                                ELSE '' END +
           CASE WHEN USER_TYPE = 'GEOGRAPHY' THEN ', [' + c.COLUMN_NAME + '.STSrid] INT' ELSE '' END, ',' + CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  JOIN (SELECT CASE WHEN SCHEMA_NAME(st.[schema_id]) IN ('sys', 'dbo')
                    THEN '' ELSE SCHEMA_NAME(st.[schema_id]) + '.' END + st.[name] AS USER_TYPE, st.user_type_id
          FROM sys.types st WITH (NOLOCK)) st ON st.user_type_id = sc.user_type_id
  LEFT JOIN sys.xml_schema_collections xc WITH (NOLOCK) ON xc.xml_collection_id = sc.xml_collection_id
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = '{tableSchema}' AND c.TABLE_NAME = '{tableName}'
    AND cc.[name] IS NULL
@";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static bool CheckIdentityInsertRequired(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT CAST(CASE WHEN EXISTS (SELECT * FROM sys.identity_columns WITH (NOLOCK) WHERE [object_id] = OBJECT_ID('{tableSchema}.{tableName}'))
                 THEN 1 ELSE 0 END AS BIT)
";
        return cmd.ExecuteScalar() as bool? ?? false;
    }

    private static string? GetInsertColumns(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT STRING_AGG('        [' + c.COLUMN_NAME + ']', ',' + CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = '{tableSchema}' AND c.TABLE_NAME = '{tableName}'
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string? GetUpdateColumns(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN c.DATA_TYPE = 'GEOGRAPHY' THEN 'G' 
                       WHEN c.DATA_TYPE = 'XML' THEN 'X' 
                       ELSE '' END + 
                  '[' + c.COLUMN_NAME + ']', ',') WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = '{tableSchema}' AND c.TABLE_NAME = '{tableName}'
    AND ident.[Name] IS NULL
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string? GetFromJsonSelectColumns(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN c.DATA_TYPE = 'GEOGRAPHY' 
                      THEN 'geography::STGeomFromText([' + c.COLUMN_NAME + '], [' + c.COLUMN_NAME + '.STSrid]) AS [' + c.COLUMN_NAME + ']'
                      ELSE '[' + c.COLUMN_NAME + ']' END, ',') WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = '{tableSchema}' AND c.TABLE_NAME = '{tableName}'
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string? GetSelectColumns(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN c.DATA_TYPE = 'GEOGRAPHY' 
                       THEN '[' + c.COLUMN_NAME + '].ToString() AS [' + c.COLUMN_NAME + '], [' + c.COLUMN_NAME + '].STSrid AS [' + c.COLUMN_NAME + '.STSrid]'
                       ELSE '[' + c.COLUMN_NAME + ']' END, ',') WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = '{tableSchema}' AND c.TABLE_NAME = '{tableName}'
    AND cc.[name] IS NULL
	AND sc.is_rowguidcol = 0
";
        return cmd.ExecuteScalar()?.ToString();
    }
}
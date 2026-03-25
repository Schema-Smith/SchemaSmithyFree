// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using log4net;
using Schema.DataAccess;
using Schema.Utility;

namespace SchemaTongs;

public class ScriptValidator
{
    private static readonly ILog Log = LogFactory.GetLogger("ProgressLog");

    // Matches: CREATE [OR ALTER] (VIEW|FUNCTION|PROCEDURE) [schema].[name]
    private static readonly Regex CreatePattern = new(
        @"CREATE\s+(OR\s+ALTER\s+)?(VIEW|FUNCTION|PROCEDURE)\s+\[(\w+)\]\.\[(\w+)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> TempPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VIEW"] = "vw_",
        ["FUNCTION"] = "fn_",
        ["PROCEDURE"] = "sp_"
    };

    public static RewriteResult RewriteWithTempName(string script)
    {
        var match = CreatePattern.Match(script);
        if (!match.Success)
            return new RewriteResult { Success = false };

        var objectType = match.Groups[2].Value.ToUpper();
        var schema = match.Groups[3].Value;
        var originalName = match.Groups[4].Value;

        var prefix = TempPrefixes.GetValueOrDefault(objectType, "tmp_");
        var tempName = $"{prefix}{Guid.NewGuid():N}"[..30];

        var rewritten = script[..match.Groups[4].Index]
            + tempName
            + script[(match.Groups[4].Index + match.Groups[4].Length)..];

        return new RewriteResult
        {
            Success = true,
            Script = rewritten,
            OriginalName = $"[{schema}].[{originalName}]",
            TempName = $"[{schema}].[{tempName}]"
        };
    }

    public static string GenerateParseOnlyWrapper(string script)
    {
        return $"SET PARSEONLY ON;\r\n{script}\r\nSET PARSEONLY OFF;";
    }

    public static ValidationResult ValidateScript(IDbConnection connection, string script, string objectType)
    {
        var isTableAttached = objectType.Equals("TRIGGER", StringComparison.OrdinalIgnoreCase);
        return isTableAttached ? ValidateParseOnly(connection, script) : ValidateGuidRename(connection, script);
    }

    private static ValidationResult ValidateGuidRename(IDbConnection connection, string script)
    {
        var rewrite = RewriteWithTempName(script);
        if (!rewrite.Success)
            return new ValidationResult { IsValid = true }; // Can't rewrite = skip validation

        var batches = SqlHelpers.SplitIntoBatches(rewrite.Script);

        using var transaction = ((DbConnection)connection).BeginTransaction();
        try
        {
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = batch;
                cmd.ExecuteNonQuery();
            }
            return new ValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            return new ValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
        finally
        {
            try { transaction.Rollback(); } catch { /* already rolled back */ }
        }
    }

    private static ValidationResult ValidateParseOnly(IDbConnection connection, string script)
    {
        var wrapped = GenerateParseOnlyWrapper(script);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = wrapped;
            cmd.ExecuteNonQuery();
            return new ValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            return new ValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }
}

public class RewriteResult
{
    public bool Success { get; set; }
    public string Script { get; set; }
    public string OriginalName { get; set; }
    public string TempName { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
}

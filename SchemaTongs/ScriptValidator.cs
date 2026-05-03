// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaTongs;

internal class ScriptValidator
{
    private static readonly ScriptObjectType[] GuidRenameTypes =
    [
        ScriptObjectType.Views, ScriptObjectType.Functions, ScriptObjectType.Procedures,
        ScriptObjectType.WindowFunctions, ScriptObjectType.Aggregates
    ];

    private static readonly ScriptObjectType[] ParseOnlyTypes =
    [
        ScriptObjectType.Triggers, ScriptObjectType.DDLTriggers,
        ScriptObjectType.TriggerFunctions, ScriptObjectType.Rules
    ];

    public RewriteResult RewriteWithTempName(string script, Platform platform)
    {
        var tempName = "_validate_" + Guid.NewGuid().ToString("N")[..12];
        var pattern = GetCreatePattern(platform);
        var match = pattern.Match(script);
        if (!match.Success)
            return new RewriteResult { Success = false };

        var nameGroup = match.Groups["name"];
        var quotedTemp = QuoteTempName(tempName, platform);
        var rewritten = script[..nameGroup.Index] + quotedTemp + script[(nameGroup.Index + nameGroup.Length)..];

        return new RewriteResult { Success = true, RewrittenScript = rewritten, TempName = tempName };
    }

    public string GenerateParseOnlyWrapper(string script, Platform platform)
    {
        return platform switch
        {
            Platform.SqlServer => $"SET PARSEONLY ON;\n{script}\nSET PARSEONLY OFF;",
            Platform.MySQL => $"PREPARE _validate_stmt FROM '{script.Replace("'", "''")}'; DEALLOCATE PREPARE _validate_stmt;",
            Platform.PostgreSQL => null,
            _ => throw new ArgumentException($"Unsupported platform: {platform}")
        };
    }

    public ValidationResult ValidateScript(IDbConnection connection, string script,
        ScriptObjectType objectType, Platform platform)
    {
        if (IsSkippedType(objectType))
            return new ValidationResult { IsValid = true, Skipped = true };

        string executableScript;
        if (Array.IndexOf(ParseOnlyTypes, objectType) >= 0)
        {
            var wrapped = GenerateParseOnlyWrapper(script, platform);
            if (wrapped == null)
                return new ValidationResult { IsValid = true, Skipped = true };
            executableScript = wrapped;
        }
        else
        {
            var rewrite = RewriteWithTempName(script, platform);
            if (!rewrite.Success)
                return new ValidationResult { IsValid = false, ErrorMessage = "Could not rewrite CREATE statement for validation" };
            executableScript = rewrite.RewrittenScript;
        }

        var batches = SplitIntoBatches(executableScript, platform);

        var transaction = connection.BeginTransaction();
        try
        {
            foreach (var batch in batches)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }
            return new ValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            return new ValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
        finally
        {
            transaction.Rollback();
        }
    }

    private static List<string> SplitIntoBatches(string script, Platform platform)
    {
        if (platform == Platform.SqlServer)
            return SqlHelpers.SplitIntoBatches(script);
        return [script];
    }

    private static bool IsSkippedType(ScriptObjectType objectType)
    {
        return Array.IndexOf(GuidRenameTypes, objectType) < 0
            && Array.IndexOf(ParseOnlyTypes, objectType) < 0;
    }

    private static Regex GetCreatePattern(Platform platform)
    {
        // The "name" group captures the full quoted object name (including quotes) for replacement
        return platform switch
        {
            Platform.SqlServer => new Regex(
                @"CREATE\s+(OR\s+ALTER\s+)?(FUNCTION|VIEW|PROCEDURE)\s+(\[\w+\]\.)?(?<name>\[\w+\]|\w+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            Platform.PostgreSQL => new Regex(
                @"CREATE\s+(OR\s+REPLACE\s+)?(FUNCTION|VIEW|PROCEDURE)\s+(" + @"""[\w]+""\.|[\w]+\.)?(?<name>""[\w]+""|[\w]+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            Platform.MySQL => new Regex(
                @"CREATE\s+(DEFINER\s*=\s*`[^`]+`@`[^`]+`\s+)?(FUNCTION|VIEW|PROCEDURE)\s+(?<name>`[\w]+`|[\w]+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            _ => throw new ArgumentException($"Unsupported platform: {platform}")
        };
    }

    private static string QuoteTempName(string tempName, Platform platform)
    {
        return platform switch
        {
            Platform.SqlServer => $"[{tempName}]",
            Platform.PostgreSQL => $"\"{tempName}\"",
            Platform.MySQL => $"`{tempName}`",
            _ => throw new ArgumentException($"Unsupported platform: {platform}")
        };
    }
}

internal class RewriteResult
{
    public bool Success { get; set; }
    public string RewrittenScript { get; set; }
    public string TempName { get; set; }
}

internal class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
    public string ObjectName { get; set; }
    public bool Skipped { get; set; }
}

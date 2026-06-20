// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using System.Text.RegularExpressions;

namespace Schema.Utility;

/// <summary>
/// Pure, stateless scrubbing of sensitive values before they reach any log surface (settings echo,
/// token logging, future CLI-argument logging, resolved-script artifacts). Behaviour is driven by a
/// baked-in default sensitive-name pattern set plus per-tool <see cref="LogHygieneOptions"/>.
/// </summary>
public static class LogScrubber
{
    public const string Mask = "***";

    // Contains-match substrings (case-insensitive). Stored without glob asterisks; the asterisk
    // syntax users write in ScrubPatterns ("*Salt*") is purely cosmetic and normalised away.
    private static readonly string[] DefaultSensitivePatterns =
        ["Password", "Pwd", "Secret", "ApiKey", "Token", "ConnectionString", "Credential"];

    // Connection-string Password=/Pwd= subfield. \b anchors to the actual key so a "MyPasswordHint="
    // key is not matched. The value alternation consumes a quoted form FIRST ("...", '...', {...} —
    // all driver-supported and able to contain ';') before falling back to the unquoted [^;]* form,
    // so a password whose value contains a semicolon is fully masked rather than truncated at it.
    private static readonly Regex ConnectionStringSecret =
        new(@"(?<key>\b(?:password|pwd)\s*=)\s*(?:""[^""]*""|'[^']*'|\{[^}]*\}|[^;]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool ShouldScrubName(string name, LogHygieneOptions options)
    {
        if (string.IsNullOrEmpty(name)) return false;
        options ??= LogHygieneOptions.Default;

        if (options.AllowTokens.Contains(name)) return false;
        if (options.ScrubTokens.Contains(name)) return true;

        return DefaultSensitivePatterns
            .Concat(options.ScrubPatterns.Select(p => p.Trim('*')).Where(p => p.Length > 0))
            .Any(name.ContainsIgnoringCase);
    }

    public static string ScrubConnectionStringSubfields(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return ConnectionStringSecret.Replace(value, m => $"{m.Groups["key"].Value}{Mask}");
    }

    public static string ScrubTokenValue(string name, string value, LogHygieneOptions options)
    {
        options ??= LogHygieneOptions.Default;
        return ShouldScrubName(name, options) ? Mask : ScrubConnectionStringSubfields(value);
    }
}

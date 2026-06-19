// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Schema.Utility;

/// <summary>
/// Per-tool log-hygiene configuration bound from the <c>LogHygiene</c> settings block. With no block
/// present the defaults apply: token logging on, only the baked-in <see cref="LogScrubber"/> patterns
/// scrubbed.
/// </summary>
public sealed class LogHygieneOptions
{
    public bool LogTokens { get; init; } = true;
    public HashSet<string> ScrubTokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ScrubPatterns { get; } = [];
    public HashSet<string> AllowTokens { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static LogHygieneOptions Default { get; } = new();

    public static LogHygieneOptions FromConfiguration(IConfiguration config)
    {
        var section = config.GetSection("LogHygiene");
        var options = new LogHygieneOptions { LogTokens = section["LogTokens"]?.ToLower() != "false" };
        foreach (var t in ReadArray(section, "ScrubTokens")) options.ScrubTokens.Add(t);
        options.ScrubPatterns.AddRange(ReadArray(section, "ScrubPatterns"));
        foreach (var t in ReadArray(section, "AllowTokens")) options.AllowTokens.Add(t);
        return options;
    }

    private static IEnumerable<string> ReadArray(IConfiguration section, string key) =>
        section.GetSection(key).GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim());
}

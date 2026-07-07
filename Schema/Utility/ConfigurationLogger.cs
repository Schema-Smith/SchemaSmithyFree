// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Schema.Utility;

public static class ConfigurationLogger
{
    public static void LogConfiguration(IConfigurationRoot config, Action<string> logLine)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                      ?? assembly.GetName().Version?.ToString()
                      ?? "unknown";
        logLine?.Invoke($"Version: {version}");

        var entries = config
            .GetChildren().Where(s => !s.Key.EqualsIgnoringCase("Description"))
            .SelectMany(s => config.GetSection(s.Key).AsEnumerable())
            .OrderBy(s => PadArrayIndexInKey(s.Key)) // preserve the actual order of array items
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        logLine?.Invoke("Configuration:");
        var hygiene = LogHygieneOptions.FromConfiguration(config);
        var arrayNameKeys = new HashSet<string>();
        foreach (var entry in entries)
        {
            var key = entry.Key;
            if (arrayNameKeys.Contains(entry.Key)) continue; // skip array name keys we've already logged
            var indents = 1;
            while (key.Contains(':'))
            {
                indents++;
                key = TryIndexToItemName(key.Substring(key.IndexOf(":", StringComparison.Ordinal) + 1), entries, entry, arrayNameKeys);
            }
            var value = LogScrubber.ShouldScrubName(key, hygiene)
                ? LogScrubber.Mask // sensitively-named key: mask the whole value
                : LogScrubber.ScrubConnectionStringSubfields(entry.Value ?? ""); // strip any embedded connection-string password
            logLine?.Invoke($"{new string(' ', indents * 2)}{key}: {value}");
        }

        logLine?.Invoke("");
        logLine?.Invoke("");
    }

    public static void LogCommandLine(IConfigurationRoot config, Action<string> logLine)
    {
        var hygiene = LogHygieneOptions.FromConfiguration(config);
        logLine?.Invoke("Command line:");

        var switches = CommandLineParser.SwitchesAndValues;
        if (switches.Count == 0)
        {
            logLine?.Invoke("  (none)");
        }
        else
        {
            foreach (var entry in switches)
            {
                var value = LogScrubber.ScrubTokenValue(entry.Key, entry.Value ?? "", hygiene);
                logLine?.Invoke($"  {entry.Key}: {value}");
            }
        }

        logLine?.Invoke("");
        logLine?.Invoke("");
    }

    private static string TryIndexToItemName(string key, Dictionary<string, string> entries, KeyValuePair<string, string> entry, HashSet<string> arrayNameKeys)
    {
        if (int.TryParse(key, out _))
        {
            // if the key is an int and has a name subkey, we assume it's an array index and use the name instead
            if (entries.ContainsKey($"{entry.Key}:Name"))
            {
                key = entries[$"{entry.Key}:Name"];
                arrayNameKeys.Add($"{entry.Key}:Name");
            }
        }

        return key;
    }

    private static string PadArrayIndexInKey(string key)
    {
        const string pattern = @"(?<=:)\d+(?=:)";
        return Regex.Replace($"{key}:", pattern, match => $"{(int.TryParse(match.Value, out var number) ? number.ToString("D5") : match.Value)}");
    }
}

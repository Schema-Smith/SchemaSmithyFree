﻿using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System;
using System.Linq;
using System.Reflection;

namespace Schema.Utility;

public static class ConfigurationLogger
{
    public static void LogConfiguration(IConfigurationRoot config, Action<string> logLine)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        logLine?.Invoke($"Version: {FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion}");

        var entries = config
            .GetChildren().Where(s => !s.Key.EqualsIgnoringCase("Description"))
            .SelectMany(s => config.GetSection(s.Key).AsEnumerable())
            .OrderBy(s => s.Key);
        logLine?.Invoke("Configuration:");
        foreach (var entry in entries)
        {
            var key = entry.Key;
            var indents = 1;
            while (key.Contains(':'))
            {
                indents++;
                key = key.Substring(key.IndexOf(":") + 1);
            }
            logLine?.Invoke($"{new string(' ', indents * 2)}{key}: {entry.Value}");
        }

        logLine?.Invoke("");
        logLine?.Invoke("");
    }
}
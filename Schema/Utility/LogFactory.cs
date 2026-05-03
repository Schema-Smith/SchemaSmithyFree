// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using System;
using System.Collections.Concurrent;

namespace Schema.Utility;

public static class LogFactory
{
    private static bool logConfigured;
    private static readonly ConcurrentDictionary<string, ILog> NamedLoggers = new();

    private static readonly object LockObject = new();

    /// <summary>
    /// Optional initializer action called once before the first logger is created.
    /// Set this to configure log4net (e.g., via ConfigHelper.ConfigureLog4Net).
    /// </summary>
    public static Action LogInitializer { get; set; }

    public static ILog GetLogger(string name)
    {
        lock (LockObject)
        {
            if (!logConfigured)
            {
                LogInitializer?.Invoke();
                logConfigured = true;
            }

            if (NamedLoggers.TryGetValue(name, out var logger1))
            {
                return logger1;
            }
            var logger = LogManager.GetLogger(name);
            NamedLoggers[name] = logger;
            return logger;
        }
    }

    public static void Register(string name, ILog logger)
    {
        lock (LockObject)
        {
            if (NamedLoggers.ContainsKey(name))
                NamedLoggers[name] = logger;
            else
                NamedLoggers.TryAdd(name, logger);
        }
    }

    public static void Clear()
    {
        NamedLoggers.Clear();
        logConfigured = false;
    }
}

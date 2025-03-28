using log4net;
using System.Collections.Concurrent;

namespace Schema.Utility;

public static class LogFactory
{
    private static bool logConfigured;
    private static readonly ConcurrentDictionary<string, ILog> NamedLoggers = new();

    private static readonly object LockObject = new();

    public static ILog GetLogger(string name)
    {
        lock (LockObject)
        {
            if (!logConfigured)
            {
                ConfigHelper.ConfigureLog4Net();
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
    }
}
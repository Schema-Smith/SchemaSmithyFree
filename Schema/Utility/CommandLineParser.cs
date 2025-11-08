using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Schema.Isolators;

namespace Schema.Utility;

public static class CommandLineParser
{
    public static string CommandLine { get; } = ForceLeadingSpace(Environment.CommandLine);

    public static List<string> Arguments
    {
        get
        {
            var result = new List<string>();
            var pos = 0;
            while (pos < CommandLine.Length - 1)
            {
                var nextPos = FindNextUnquotedSpace(CommandLine, pos);
                if (nextPos == -1)
                {
                    result.Add(CommandLine.Substring(pos).Trim().Unquote());
                    break;
                }
                var arg = CommandLine.Substring(pos, nextPos - pos).Trim().Unquote();
                if (arg != string.Empty)
                    result.Add(arg);
                pos = FindNextNonSpace(CommandLine, nextPos);
            }

            return result;
        }
    }

    public static Dictionary<string, string> SwitchesAndValues
    {
        get
        {
            var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var argument in Arguments.Where(x => x.StartsWith("/") || x.StartsWith("-")))
            {
                // split into max 2 parts to handle values with embedded colon or equals
                var parts = argument.Split(new[] { ':', '=' }, 2);
                result[TrimKeyName(parts[0])] = parts.Length switch
                {
                    1 => string.Empty,
                    2 => parts[1],
                    _ => result[TrimKeyName(parts[0])]
                };
            }
            return result;
        }
    }

    public static bool ContainsSwitch(string switchName)
    {
        return SwitchesAndValues.ContainsKey(switchName);
    }

    public static string ValueOfSwitch(string switchName, string defval = "")
    {
        return ContainsSwitch(switchName) ? SwitchesAndValues[switchName].Trim('"', ' ') : defval;
    }

    public static int IntValueOfSwitch(string switchName, int defval = -1)
    {
        if (!int.TryParse(ValueOfSwitch(switchName), out var result))
            result = defval;
        return result;
    }

    public static void HandleCommonSwitches(string app)
    {
        if (ContainsSwitch("v") || ContainsSwitch("ver") || ContainsSwitch("version")) ShowVersionAndExit(app);
        if (ContainsSwitch("?") || ContainsSwitch("h") || ContainsSwitch("help")) ShowHelpAndExit(app);
    }

    private static string ForceLeadingSpace(string commandLine)
    {
        if (!commandLine.StartsWith(" "))
            commandLine = " " + commandLine;
        return commandLine;
    }

    private static int FindNextNonSpace(string s, int startPos)
    {
        var curPos = startPos;
        while (s[curPos] == ' ' && curPos < s.Length - 1)
            curPos++;
        return curPos;
    }

    private static int FindNextUnquotedSpace(string s, int startPos)
    {
        var firstQuote = s.IndexOf('"', startPos);
        var nextQuote = s.IndexOf('"', firstQuote + 1);
        var nextSpace = s.IndexOf(' ', startPos);
        if (nextSpace == -1) return nextSpace;
        while (nextSpace > firstQuote && nextSpace < nextQuote)
            nextSpace = FindNextUnquotedSpace(s, nextQuote + 1);

        return nextSpace;
    }

    private static string TrimKeyName(string s)
    {
        return s.TrimStart('/').TrimStart('-');
    }

    private static void ShowVersionAndExit(string app)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        Console.WriteLine($"{app} MSSQL Community - Version: {FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion}");
        EnvironmentWrapper.GetFromFactory().Exit(0);
    }

    private static void ShowHelpAndExit(string app)
    {
        Console.WriteLine($"{app}.exe [<command>]");
        Console.WriteLine("  --version                Show the program version");
        Console.WriteLine("  --LogPath:<logpath>      Path to write logs and create backup directories. The default is current path.");
        Console.WriteLine("  --ConfigFile:<filepath>  Path and file name of the config file. The default is appsettings.json in the current path.");
        Console.WriteLine("  --help                   Show the command line options");
        EnvironmentWrapper.GetFromFactory().Exit(0);
    }
}
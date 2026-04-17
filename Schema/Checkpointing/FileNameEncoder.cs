// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Text;

namespace Schema.Checkpointing;

internal static class FileNameEncoder
{
    private static readonly char[] IllegalChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string Encode(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var sb = new StringBuilder(name.Length);

        foreach (var c in name)
        {
            if (c == '%')
                sb.Append("%25");
            else if (Array.IndexOf(IllegalChars, c) >= 0)
                sb.Append($"%{(byte)c:X2}");
            else
                sb.Append(c);
        }

        var result = sb.ToString();

        var leadEncoded = new StringBuilder();
        var idx = 0;
        while (idx < result.Length && result[idx] is ' ' or '.')
        {
            leadEncoded.Append(result[idx] == ' ' ? "%20" : "%2E");
            idx++;
        }
        if (idx > 0)
            result = leadEncoded + result[idx..];

        var trailEncoded = new StringBuilder();
        var endIdx = result.Length - 1;
        while (endIdx >= 0 && result[endIdx] is ' ' or '.')
        {
            trailEncoded.Insert(0, result[endIdx] == ' ' ? "%20" : "%2E");
            endIdx--;
        }
        if (endIdx < result.Length - 1)
            result = result[..(endIdx + 1)] + trailEncoded;

        if (ReservedNames.Contains(name))
            result = $"%{(byte)name[0]:X2}" + result[1..];

        return result;
    }
}

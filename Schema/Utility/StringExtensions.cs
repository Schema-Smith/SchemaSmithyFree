using System.Text.RegularExpressions;
using System;

namespace Schema.Utility;

public static class StringExtensions
{
    public static bool ContainsIgnoringCase(this string source, string s) => source.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0;

    public static bool EndsWithIgnoringCase(this string source, string s) => source.EndsWith(s, StringComparison.InvariantCultureIgnoreCase);

    public static bool EqualsIgnoringCase(this string source, string s) => source.Equals(s, StringComparison.InvariantCultureIgnoreCase);

    public static int IndexOfIgnoringCase(this string source, string s) => source.IndexOf(s, StringComparison.InvariantCultureIgnoreCase);

    public static int IndexOfIgnoringCase(this string source, string s, int startIndex) => source.IndexOf(s, startIndex, StringComparison.InvariantCultureIgnoreCase);

    public static bool StartsWithIgnoringCase(this string source, string s) => source.StartsWith(s, StringComparison.InvariantCultureIgnoreCase);

    public static string ReplaceIgnoringCase(this string source, string find, string replace) => Regex.Replace(source, Regex.Escape(find), replace, RegexOptions.IgnoreCase);
}

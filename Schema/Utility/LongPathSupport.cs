// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;

namespace Schema.Utility;

public static class LongPathSupport
{
    private static readonly bool _isLinux = Path.DirectorySeparatorChar == '/';

    public static string MakeSafeLongFilePath(string path, bool? overrideIsLinux = null)
    {
        var isLinux = overrideIsLinux ?? _isLinux;
        // if this is running on linux there is no need to do special handling and if it already has the long path prefix or there are no path characters then there is nothing to do
        if (isLinux || path.StartsWith(@"\\?\") || path.StartsWith(".") || !(path.Contains(@"\") || path.Contains("/") || path.Contains(":")))
            return path;

        // Windows supports both the windows and unix style path separators... normalize to windows style for simplicity.
        // Use literal chars (not Path.*DirectorySeparatorChar) so the logic stays correct when overrideIsLinux forces
        // Windows behavior on a non-Windows runtime (unit tests / Linux CI).
        path = path.Replace('/', '\\');

        // UNC path -> \\?\UNC\server\share
        if (path.StartsWith(@"\\"))
            return $@"\\?\UNC\{path.Substring(2)}";

        // The \\?\ prefix requires a FULLY-QUALIFIED path. Only a rooted drive path (e.g. C:\dir) qualifies; a
        // relative path (e.g. "Package\Product.json") must be returned unprefixed so the File/Directory APIs resolve
        // it against the current directory. Prefixing a relative path yields "\\?\Package\..." which Windows silently
        // reports as non-existent — the root cause of relative --Source / Product:Path failing on Windows.
        // Sadly the long-path handling below is still not the default for .NET, but it makes File.Copy et al. handle
        // long path names, and it is safe for short paths too:
        //     https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=registry
        if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '\\')
            return $@"\\?\{path}";

        return path;
    }

    public static string StripLongPathPrefix(string path)
    {
        return path.Replace(@"\\?\UNC\", @"\\").Replace(@"\\?\", "");
    }
}

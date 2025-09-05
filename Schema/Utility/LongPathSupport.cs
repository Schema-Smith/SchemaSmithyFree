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

        // Windows supports both the windows and unix style path separators... normalize to windows style for simplicity
        path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        // Sadly this is still not the default behavior for .NET in VS 2022, but this is how to make File.Copy handle long path names, and it
        // seems to be safe for short paths as well so we can force the long file path handling behavior for all file access
        //     https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=registry
        return path.StartsWith(@"\\") ? $@"\\?\UNC\{path.Substring(2)}" : $@"\\?\{path}";
    }

    public static string StripLongPathPrefix(string path)
    {
        return path.Replace(@"\\?\UNC\", @"\\").Replace(@"\\?\", "");
    }
}
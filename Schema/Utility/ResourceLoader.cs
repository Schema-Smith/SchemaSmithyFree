using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Schema.Utility;

public static class ResourceLoader
{
    public static string Load(string fileName)
    {
        using var s = GetResourceStreamByFullyQualifiedFileName(fileName, Assembly.GetCallingAssembly());
        using var sr = new StreamReader(s);
        return sr.ReadToEnd();
    }

    private static Stream GetResourceStreamByFullyQualifiedFileName(string fileName, Assembly fromAssembly)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var resourceNames = fromAssembly.GetManifestResourceNames();

        var fullyQualifiedFileName = (from a in resourceNames
                                      where a.Trim().ToUpper().EndsWith("." + fileName.Trim().ToUpper())
                                      select a).FirstOrDefault();

        if (string.IsNullOrEmpty(fullyQualifiedFileName))
            throw new FileLoadException($"File {fileName} not found in assembly {fromAssembly.FullName}.");

        return fromAssembly.GetManifestResourceStream(fullyQualifiedFileName);
    }
}
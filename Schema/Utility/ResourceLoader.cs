using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Schema.Utility;

public static class ResourceLoader
{
    public static string Load(string fileName)
    {
        using var s = GetResourceStreamByFullyQualifiedFileName(fileName);
        using var sr = new StreamReader(s);
        return sr.ReadToEnd();
    }

    private static Stream GetResourceStreamByFullyQualifiedFileName(string fileName)
    {
        var fromAssembly = Assembly.GetCallingAssembly();
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var resourceNames = fromAssembly.GetManifestResourceNames();

        var fullyQualifiedFileName = (from a in resourceNames
                                      where a.Trim().ToUpper().EndsWith("." + fileName.Trim().ToUpper())
                                      select a).FirstOrDefault();
        if (!string.IsNullOrEmpty(fullyQualifiedFileName)) return fromAssembly.GetManifestResourceStream(fullyQualifiedFileName);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a != fromAssembly && !a.IsDynamic);
        foreach (var assembly in assemblies)
        {
            resourceNames = assembly.GetManifestResourceNames();
            fullyQualifiedFileName = (from a in resourceNames where a.Trim().ToUpper().EndsWith("." + fileName.Trim().ToUpper()) select a).FirstOrDefault();
            if (!string.IsNullOrEmpty(fullyQualifiedFileName)) return assembly.GetManifestResourceStream(fullyQualifiedFileName);
        }
        
        throw new FileLoadException($"File {fileName} not found in assembly {fromAssembly.FullName}.");
    }
}
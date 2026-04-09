// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Schema.Domain;

namespace Schema.Utility;

public static class ResourceLoader
{
    public static string Load(string fileName)
    {
        using var s = GetResourceStreamByFullyQualifiedFileName(fileName, Assembly.GetCallingAssembly());
        using var sr = new StreamReader(s);
        return sr.ReadToEnd();
    }

    public static string Load(string scriptName, Platform platform)
    {
        var assembly = typeof(ResourceLoader).Assembly;
        var stream = GetPlatformResourceStream(scriptName, platform, assembly);
        if (stream == null)
            return null;
        using (stream)
        using (var sr = new StreamReader(stream))
            return sr.ReadToEnd();
    }

    public static Stream ToStream(string fileName)
    {
        return GetResourceStreamByFullyQualifiedFileName(fileName, Assembly.GetCallingAssembly());
    }

    public static byte[] LoadBytes(string fileName)
    {
        using var s = GetResourceStreamByFullyQualifiedFileName(fileName, Assembly.GetCallingAssembly());
        using var memoryStream = new MemoryStream();
        s.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    internal static Stream GetPlatformResourceStream(string scriptName, Platform platform, Assembly assembly)
    {
        var resourceNames = assembly.GetManifestResourceNames();
        var scriptNameUpper = scriptName.Trim().ToUpper();

        // Try variant-specific folder first (e.g., Scripts/SqlServer.Azure/scriptName)
        var basePlatform = platform.GetBasePlatform();
        if (basePlatform != platform)
        {
            var variantSuffix = $".Scripts.{platform}.{scriptName}".ToUpper();
            var variantMatch = resourceNames.FirstOrDefault(r => r.ToUpper().EndsWith(variantSuffix));
            if (variantMatch != null)
                return assembly.GetManifestResourceStream(variantMatch);
        }

        // Try base platform folder (e.g., Scripts/SqlServer/scriptName)
        var baseSuffix = $".Scripts.{basePlatform}.{scriptName}".ToUpper();
        var baseMatch = resourceNames.FirstOrDefault(r => r.ToUpper().EndsWith(baseSuffix));
        if (baseMatch != null)
            return assembly.GetManifestResourceStream(baseMatch);

        return null;
    }

    private static Stream GetResourceStreamByFullyQualifiedFileName(string fileName, Assembly fromAssembly)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var resourceNames = fromAssembly.GetManifestResourceNames();

        var fullyQualifiedFileName = (from a in resourceNames
                                      where a.Trim().ToUpper().EndsWith("." + fileName.Trim().ToUpper())
                                      select a).FirstOrDefault();
        if (!string.IsNullOrEmpty(fullyQualifiedFileName)) return fromAssembly.GetManifestResourceStream(fullyQualifiedFileName)!;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a != fromAssembly && !a.IsDynamic);
        foreach (var assembly in assemblies)
        {
            resourceNames = assembly.GetManifestResourceNames();
            fullyQualifiedFileName = (from a in resourceNames where a.Trim().ToUpper().EndsWith("." + fileName.Trim().ToUpper()) select a).FirstOrDefault();
            if (!string.IsNullOrEmpty(fullyQualifiedFileName)) return assembly.GetManifestResourceStream(fullyQualifiedFileName)!;
        }

        throw new FileLoadException($"File {fileName} not found in assembly {fromAssembly.FullName}.");
    }
}

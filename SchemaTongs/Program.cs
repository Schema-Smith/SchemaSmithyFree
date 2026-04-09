// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using Schema.Domain;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;

namespace SchemaTongs;

public static class Program
{
    public static void Main(string[] args)
    {
        CommandLineParser.HandleCommonSwitches("SchemaTongs", ToolSpecificSwitches);

        // Register Community defaults — Pro packages register replacements via module initializers.
        FactoryContainer.Register<ISchemaLicense>(new NullSchemaLicense());

        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        LogFactory.LogInitializer = ConfigHelper.ConfigureLog4Net;
        ConfigHelper.GetAppSettingsAndUserSecrets("SchemaTongs", LogFactory.GetLogger("ProgressLog").Info);

        Console.WriteLine(ToolHelpFormatter.GetLicenseDisplayText());

        if (CommandLineParser.ContainsSwitch("WriteSchemasOnly"))
        {
            WriteSchemasOnly();
            return;
        }

        var platform = ResolvePlatform();

        new SchemaTongs(platform).CastTemplate();
        LogBackup.BackupLogsAndExit("SchemaTongs");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaTongs", e);
    }

    /// <summary>
    /// Regenerates the .json-schemas/*.schema validation files for an existing product
    /// without connecting to a database. Reads the platform from Product.json and writes
    /// the schemas on the fly from the current C# domain types.
    /// </summary>
    internal static void WriteSchemasOnly()
    {
        var config = FactoryContainer.ResolveOrCreate<Microsoft.Extensions.Configuration.IConfigurationRoot>();
        var productPath = config["Product:Path"] ?? ".";
        var productFile = Path.Combine(productPath, "Product.json");

        if (!File.Exists(productFile))
            throw new Exception($"Product.json not found at '{productFile}'. Set 'Product:Path' to the product directory.");

        var product = Product.LoadForDisplay(productFile);
        Console.WriteLine($"Regenerating .json-schemas for {product.Name} ({product.Platform})...");
        RepositoryHelper.WriteSchemaFiles(productPath, product.Platform);
        Console.WriteLine("Done.");
    }

    internal static Platform ResolvePlatform()
    {
        var config = FactoryContainer.ResolveOrCreate<Microsoft.Extensions.Configuration.IConfigurationRoot>();
        var platformValue = config["Target:Platform"] ?? config["Source:Platform"];
        if (string.IsNullOrWhiteSpace(platformValue))
            throw new Exception("Platform is required. Set 'Target:Platform' or 'Source:Platform' in SchemaTongs.settings.json.");

        return PlatformExtensions.ParsePlatform(platformValue);
    }

    private static void ToolSpecificSwitches()
    {
        Console.WriteLine("  --WriteSchemasOnly               Regenerate .json-schemas for an existing product without connecting to a database.");
        var proOptions = ToolHelpFormatter.FormatProOptions("SchemaTongs");
        if (!string.IsNullOrEmpty(proOptions))
        {
            Console.WriteLine();
            Console.WriteLine("Pro options:");
            Console.Write(proOptions);
        }
    }
}

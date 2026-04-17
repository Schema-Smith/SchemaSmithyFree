// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Isolators;

namespace Schema.Delivery;

/// <summary>
/// IDataDeliveryConfigurator implementation that writes data delivery settings
/// into table JSON files after DataTongs extraction. Uses JObject manipulation
/// to avoid Schema domain type dependencies.
/// </summary>
public class DataDeliveryConfiguratorImpl : IDataDeliveryConfigurator
{
    public static IDataDeliveryConfigurator GetFromFactory()
        => FactoryContainer.ResolveOrCreate<IDataDeliveryConfigurator, DataDeliveryConfiguratorImpl>();

    public void Configure(DataDeliveryConfiguratorContext context)
    {
        if (context == null) return;

        var tableJsonFile = FindTableJsonFile(context.TemplateRootPath, context.TableSchema, context.TableName);
        if (tableJsonFile == null)
        {
            var displayName = string.IsNullOrEmpty(context.TableSchema) ? context.TableName : $"{context.TableSchema}.{context.TableName}";
            context.WarningLog?.Invoke($"    Table.json not found for {displayName} in {Path.Combine(context.TemplateRootPath ?? "", "Tables")}. Skipping data delivery configuration.");
            return;
        }

        var json = File.ReadAllText(tableJsonFile);
        var table = JObject.Parse(json);
        var changed = false;

        var relativePath = Path.GetRelativePath(context.TemplateRootPath, Path.GetFullPath(context.ContentFilePath)).Replace('\\', '/');
        changed |= SetIfDifferent(table, "ContentFile", relativePath, context.WarningLog,
            () => $"    ContentFile for '{context.TableName}' changed from '{table["ContentFile"]}' to '{relativePath}'.");

        var mergeType = string.IsNullOrWhiteSpace(context.MergeTypeOverride) ? context.DefaultMergeType : context.MergeTypeOverride;
        changed |= SetIfDifferent(table, "MergeType", mergeType);

        var matchColumns = string.IsNullOrWhiteSpace(context.KeyColumnsOverride) ? null : context.KeyColumnsOverride;
        changed |= SetIfDifferent(table, "MatchColumns", matchColumns);

        var mergeFilter = string.IsNullOrWhiteSpace(context.MergeFilterOverride) ? null : context.MergeFilterOverride;
        changed |= SetIfDifferent(table, "MergeFilter", mergeFilter);

        changed |= SetBoolIfDifferent(table, "MergeDisableTriggers", context.DisableTriggers);

        if (context.Platform?.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true)
        {
            changed |= SetBoolIfDifferent(table, "MergeDisableRules", context.DisableRules);
            changed |= SetBoolIfDifferent(table, "MergeUpdateDescendents", context.UpdateDescendents);
        }

        if (changed)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };
            File.WriteAllText(tableJsonFile, table.ToString(Formatting.Indented));
            context.ProgressLog?.Invoke($"    Updated data delivery config for {context.TableName}");
        }
    }

    internal static string FindTableJsonFile(string templateRootPath, string tableSchema, string tableName)
    {
        if (string.IsNullOrEmpty(templateRootPath) || string.IsNullOrEmpty(tableName))
            return null;

        var tablesDir = Path.Combine(templateRootPath, "Tables");
        if (!Directory.Exists(tablesDir))
            return null;

        var candidates = new[]
        {
            Path.Combine(tablesDir, $"{tableName}.json"),
            Path.Combine(tablesDir, $"{tableSchema}.{tableName}.json")
        };

        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;

        var files = Directory.GetFiles(tablesDir, "*.json");
        var targetName = string.IsNullOrEmpty(tableSchema) ? tableName : $"{tableSchema}.{tableName}";

        return files.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(f).Equals(tableName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool SetIfDifferent(JObject obj, string propertyName, string value,
        Action<string> warningLog = null, Func<string> warningMessage = null)
    {
        var current = obj[propertyName]?.ToString();
        if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase))
            return false;

        if (warningLog != null && warningMessage != null && !string.IsNullOrEmpty(current))
            warningLog(warningMessage());

        if (string.IsNullOrEmpty(value))
            obj.Remove(propertyName);
        else
            obj[propertyName] = value;

        return true;
    }

    private static bool SetBoolIfDifferent(JObject obj, string propertyName, bool value)
    {
        var current = obj[propertyName]?.Value<bool>() ?? false;
        if (current == value)
            return false;

        if (value)
            obj[propertyName] = value;
        else
            obj.Remove(propertyName);

        return true;
    }
}

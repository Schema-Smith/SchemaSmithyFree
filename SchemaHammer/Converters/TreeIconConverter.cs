// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SchemaHammer.Converters;

public class TreeIconConverter : IMultiValueConverter
{
    public static readonly TreeIconConverter Instance = new();

    private static readonly Dictionary<string, string> TagToColorResource = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Product"] = "SH.TreeView.ProductIconColor",
        ["Template"] = "SH.TreeView.TemplateIconColor",
        ["Table"] = "SH.TreeView.TableIconColor",
        ["Column"] = "SH.TreeView.ColumnIconColor",
        ["Index"] = "SH.TreeView.IndexIconColor",
        ["Xml Index"] = "SH.TreeView.IndexIconColor",
        ["Foreign Key"] = "SH.TreeView.ForeignKeyIconColor",
        ["Check Constraint"] = "SH.TreeView.CheckConstraintIconColor",
        ["Statistic"] = "SH.TreeView.StatisticIconColor",
        ["Full Text Index"] = "SH.TreeView.IndexIconColor",
        ["Indexed View"] = "SH.TreeView.TableIconColor",
        ["Sql Script"] = "SH.TreeView.ScriptIconColor",
        ["Sql Error Script"] = "SH.TreeView.ErrorScriptIconColor",
    };

    private static readonly Dictionary<string, string> TagToIconResource = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Product"] = "ProductIcon",
        ["Template"] = "TemplateIcon",
        ["Table"] = "TableIcon",
        ["Column"] = "ColumnIcon",
        ["Index"] = "IndexIcon",
        ["Xml Index"] = "XmlIndexIcon",
        ["Foreign Key"] = "ForeignKeyIcon",
        ["Check Constraint"] = "CheckConstraintIcon",
        ["Statistic"] = "StatisticIcon",
        ["Full Text Index"] = "FullTextIndexIcon",
        ["Indexed View"] = "IndexedViewIcon",
        ["Sql Script"] = "ScriptIcon",
        ["Sql Error Script"] = "ErrorScriptIcon",
    };

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return null;

        var imageKey = values[0] as string ?? "folder";
        var tag = values[1] as string ?? "";

        try
        {
            var geometry = GetGeometry(imageKey, tag);
            if (geometry == null) return null;

            var brush = GetBrush(tag, imageKey);

            return new DrawingImage(new GeometryDrawing
            {
                Geometry = geometry,
                Brush = brush,
                Pen = new Pen(brush, 0.5)
            });
        }
        catch
        {
            return null;
        }
    }

    private static Geometry? GetGeometry(string imageKey, string tag)
    {
        if (TagToIconResource.TryGetValue(tag, out var iconKey))
        {
            if (Application.Current?.Resources.TryGetResource(iconKey, null, out var res) == true
                && res is StreamGeometry sg)
                return sg;
        }

        var fallbackKey = imageKey switch
        {
            "product" => "ProductIcon",
            "template" => "TemplateIcon",
            "file" or "error-file" => "FileIcon",
            _ => "FolderIcon"
        };

        if (Application.Current?.Resources.TryGetResource(fallbackKey, null, out var fallback) == true
            && fallback is StreamGeometry fallbackGeometry)
            return fallbackGeometry;

        return null;
    }

    private static IBrush GetBrush(string tag, string imageKey)
    {
        if (TagToColorResource.TryGetValue(tag, out var resourceKey))
        {
            if (Application.Current?.Resources.TryGetResource(resourceKey, null, out var res) == true
                && res is IBrush brush)
                return brush;
        }

        var fallbackKey = imageKey == "file"
            ? "SH.TreeView.FileIconColor"
            : "SH.TreeView.FolderIconColor";

        if (Application.Current?.Resources.TryGetResource(fallbackKey, null, out var fallbackRes) == true
            && fallbackRes is IBrush fallbackBrush)
            return fallbackBrush;

        return new SolidColorBrush(Color.FromRgb(160, 174, 192));
    }
}

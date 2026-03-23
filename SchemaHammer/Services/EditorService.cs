// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.Services;

public class EditorService : IEditorService
{
    private readonly Dictionary<string, EditorBaseViewModel> _editorCache = new();

    public string? GetEditorTag(string nodeTag)
    {
        if (string.IsNullOrEmpty(nodeTag)) return null;

        if (nodeTag.EndsWith(" Container", StringComparison.OrdinalIgnoreCase) ||
            nodeTag.EndsWith(" Folder", StringComparison.OrdinalIgnoreCase) ||
            nodeTag.EndsWith(" FolderContainer", StringComparison.OrdinalIgnoreCase))
        {
            return "Container";
        }

        return nodeTag switch
        {
            "Product" => "Product",
            "Template" => "Template",
            "Templates" or "Tables" or "Indexed Views" => "Container",
            "Table" => "Table",
            "Column" => "Column",
            "Index" => "Index",
            "Xml Index" => "Xml Index",
            "Foreign Key" => "Foreign Key",
            "Check Constraint" => "Check Constraint",
            "Statistic" => "Statistic",
            "Full Text Index" => "Full Text Index",
            "Indexed View" => "Indexed View",
            "Sql Script" => "Sql Script",
            _ => null
        };
    }

    public EditorBaseViewModel? GetEditor(TreeNodeModel node)
    {
        var editorTag = GetEditorTag(node.Tag);
        if (editorTag == null) return null;

        if (_editorCache.TryGetValue(editorTag, out var existing))
        {
            existing.ChangeNode(node);
            return existing;
        }

        var editor = CreateEditor(editorTag);
        if (editor == null) return null;

        editor.ChangeNode(node);
        _editorCache[editorTag] = editor;
        return editor;
    }

    private static EditorBaseViewModel? CreateEditor(string editorTag)
    {
        return editorTag switch
        {
            "Product" => new ProductEditorViewModel(),
            "Template" => new TemplateEditorViewModel(),
            "Table" => new TableEditorViewModel(),
            "Column" => new ColumnEditorViewModel(),
            "Index" => new IndexEditorViewModel(),
            "Xml Index" => new XmlIndexEditorViewModel(),
            "Foreign Key" => new ForeignKeyEditorViewModel(),
            "Check Constraint" => new CheckConstraintEditorViewModel(),
            "Statistic" => new StatisticEditorViewModel(),
            "Full Text Index" => new FullTextIndexEditorViewModel(),
            "Indexed View" => new IndexedViewEditorViewModel(),
            "Sql Script" => new SqlScriptEditorViewModel(),
            "Container" => new ContainerEditorViewModel(),
            _ => null
        };
    }
}

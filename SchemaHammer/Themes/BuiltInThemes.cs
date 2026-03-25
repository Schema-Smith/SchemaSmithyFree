// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaHammer.Themes;

public static class BuiltInThemes
{
    private static readonly ThemeDefinition LightTheme = CreateLight();
    private static readonly ThemeDefinition DarkTheme = CreateDark();

    public static ThemeDefinition Light => LightTheme;
    public static ThemeDefinition Dark => DarkTheme;

    private static ThemeDefinition CreateLight() => new()
    {
        Name = "Light",
        BasedOn = "Light",
        Colors = new Dictionary<string, string>
        {
            // General
            ["SH.General.AppBackground"] = "#f0f1f4",
            ["SH.General.AppForeground"] = "#2d3139",
            ["SH.General.SelectionHighlight"] = "#3CE85D04",
            ["SH.General.GridSplitter"] = "#FFcbd5e0",
            ["SH.General.StatusBarBackground"] = "#FFE85D04",
            ["SH.General.StatusBarForeground"] = "#FFFFFFFF",
            ["SH.General.AlternatingRow"] = "#0C000000",
            ["SH.General.LoadingOverlay"] = "#80000000",
            ["SH.General.LoadingDialog"] = "#FFf0f1f4",

            // Toolbar
            ["SH.Toolbar.Background"] = "#3d424d",
            ["SH.Toolbar.Foreground"] = "#e2e8f0",

            // Tree Panel
            ["SH.TreePanel.Background"] = "#3d424d",
            ["SH.TreePanel.Foreground"] = "#e2e8f0",
            ["SH.TreePanel.SelectedBackground"] = "#26E85D04",
            ["SH.TreePanel.HoverBackground"] = "#14E85D04",
            ["SH.TreePanel.SelectedBorder"] = "#E85D04",

            // Accents
            ["SH.Accent.Ember"] = "#E85D04",
            ["SH.Accent.EmberLight"] = "#F48C06",
            ["SH.Accent.SectionHeader"] = "#E85D04",
            ["SH.Accent.EditorTitle"] = "#E85D04",

            // SQL Editor
            ["SH.SqlEditor.Background"] = "#f7fafc",
            ["SH.SqlEditor.Foreground"] = "#2d3139",
            ["SH.SqlEditor.LineNumberColor"] = "#E85D04",
            ["SH.SqlEditor.KeywordColor"] = "#0000FF",
            ["SH.SqlEditor.StringColor"] = "#FF0000",
            ["SH.SqlEditor.CommentColor"] = "#008000",
            ["SH.SqlEditor.OperatorColor"] = "#808080",
            ["SH.SqlEditor.FunctionColor"] = "#FF00FF",
            ["SH.SqlEditor.DatabaseObjectColor"] = "#008000",
            ["SH.SqlEditor.StoredProcedureColor"] = "#800000",
            ["SH.SqlEditor.GlobalVariableColor"] = "#FF00FF",
            ["SH.SqlEditor.NumberColor"] = "#000000",

            // Token Highlight
            ["SH.Token.Background"] = "#FFFF00",
            ["SH.Token.Border"] = "#C8C800",
            ["SH.Token.Foreground"] = "#000000",

            // Tree View icon colors
            ["SH.TreeView.ProductIconColor"] = "#F48C06",
            ["SH.TreeView.TemplateIconColor"] = "#FFBA08",
            ["SH.TreeView.TableIconColor"] = "#4EC9B0",
            ["SH.TreeView.ColumnIconColor"] = "#A0AEC0",
            ["SH.TreeView.IndexIconColor"] = "#FFBA08",
            ["SH.TreeView.ForeignKeyIconColor"] = "#F48C06",
            ["SH.TreeView.CheckConstraintIconColor"] = "#F48771",
            ["SH.TreeView.StatisticIconColor"] = "#A0AEC0",
            ["SH.TreeView.ScriptIconColor"] = "#F48C06",
            ["SH.TreeView.ErrorScriptIconColor"] = "#E53E3E",
            ["SH.TreeView.FolderIconColor"] = "#E8AB53",
            ["SH.TreeView.FileIconColor"] = "#A0AEC0"
        }
    };

    private static ThemeDefinition CreateDark() => new()
    {
        Name = "Dark",
        BasedOn = "Dark",
        Colors = new Dictionary<string, string>
        {
            // General
            ["SH.General.AppBackground"] = "#0f0f17",
            ["SH.General.AppForeground"] = "#D4D4D4",
            ["SH.General.SelectionHighlight"] = "#3CE85D04",
            ["SH.General.GridSplitter"] = "#FF2D3139",
            ["SH.General.StatusBarBackground"] = "#FFE85D04",
            ["SH.General.StatusBarForeground"] = "#FFFFFFFF",
            ["SH.General.AlternatingRow"] = "#08FFFFFF",
            ["SH.General.LoadingOverlay"] = "#80000000",
            ["SH.General.LoadingDialog"] = "#FF1a1a2e",

            // Toolbar
            ["SH.Toolbar.Background"] = "#0f0f17",
            ["SH.Toolbar.Foreground"] = "#e2e8f0",

            // Tree Panel
            ["SH.TreePanel.Background"] = "#0f0f17",
            ["SH.TreePanel.Foreground"] = "#e2e8f0",
            ["SH.TreePanel.SelectedBackground"] = "#26E85D04",
            ["SH.TreePanel.HoverBackground"] = "#14E85D04",
            ["SH.TreePanel.SelectedBorder"] = "#E85D04",

            // Accents
            ["SH.Accent.Ember"] = "#E85D04",
            ["SH.Accent.EmberLight"] = "#F48C06",
            ["SH.Accent.SectionHeader"] = "#F48C06",
            ["SH.Accent.EditorTitle"] = "#F48C06",

            // SQL Editor
            ["SH.SqlEditor.Background"] = "#0f0f17",
            ["SH.SqlEditor.Foreground"] = "#D4D4D4",
            ["SH.SqlEditor.LineNumberColor"] = "#F48C06",
            ["SH.SqlEditor.KeywordColor"] = "#569CD6",
            ["SH.SqlEditor.StringColor"] = "#CE9178",
            ["SH.SqlEditor.CommentColor"] = "#6A9955",
            ["SH.SqlEditor.OperatorColor"] = "#B4B4B4",
            ["SH.SqlEditor.FunctionColor"] = "#C586C0",
            ["SH.SqlEditor.DatabaseObjectColor"] = "#4EC9B0",
            ["SH.SqlEditor.StoredProcedureColor"] = "#C586C0",
            ["SH.SqlEditor.GlobalVariableColor"] = "#9CDCFE",
            ["SH.SqlEditor.NumberColor"] = "#B5CEA8",

            // Token Highlight
            ["SH.Token.Background"] = "#3D5C3A",
            ["SH.Token.Border"] = "#4E6D4C",
            ["SH.Token.Foreground"] = "#FFD700",

            // Tree View icon colors
            ["SH.TreeView.ProductIconColor"] = "#F48C06",
            ["SH.TreeView.TemplateIconColor"] = "#FFBA08",
            ["SH.TreeView.TableIconColor"] = "#4EC9B0",
            ["SH.TreeView.ColumnIconColor"] = "#A0AEC0",
            ["SH.TreeView.IndexIconColor"] = "#FFBA08",
            ["SH.TreeView.ForeignKeyIconColor"] = "#F48C06",
            ["SH.TreeView.CheckConstraintIconColor"] = "#F48771",
            ["SH.TreeView.StatisticIconColor"] = "#A0AEC0",
            ["SH.TreeView.ScriptIconColor"] = "#F48C06",
            ["SH.TreeView.ErrorScriptIconColor"] = "#FC8181",
            ["SH.TreeView.FolderIconColor"] = "#E8AB53",
            ["SH.TreeView.FileIconColor"] = "#A0AEC0"
        }
    };
}

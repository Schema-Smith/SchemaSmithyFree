// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using CommunityToolkit.Mvvm.ComponentModel;
using SchemaHammer.Models;
using System;

namespace SchemaHammer.ViewModels.Editors;

public abstract partial class EditorBaseViewModel : ObservableObject
{
    public abstract string EditorTitle { get; }
    public TreeNodeModel? Node { get; protected set; }

    /// <summary>
    /// When set, the editor should switch to the Script Tokens tab and select this token.
    /// Set by search/token navigation before switching nodes; cleared after use.
    /// </summary>
    public static string? PendingTokenName { get; set; }

    /// <summary>
    /// Callback set by MainWindowViewModel to navigate to a specific tree node.
    /// Used by token double-click navigation to jump to the token's definition.
    /// </summary>
    public Action<TreeNodeModel>? NavigateToNode { get; set; }

    [ObservableProperty]
    private string _editorLabel = "";

    public virtual void ChangeNode(TreeNodeModel node)
    {
        Node = node;
        EditorLabel = node.Text;
        OnPropertyChanged(nameof(EditorTitle));
    }

    /// <summary>Strips SQL Server bracket quoting from an identifier name: [Name] → Name</summary>
    internal static string StripBrackets(string? name)
    {
        return name?.Trim('[', ']') ?? "";
    }

    /// <summary>Checks whether a domain object name (possibly bracketed) matches tree node text (unbracketed).</summary>
    internal static bool NameMatchesNodeText(string domainName, string nodeText)
    {
        return StripBrackets(domainName).Equals(nodeText, StringComparison.OrdinalIgnoreCase);
    }
}

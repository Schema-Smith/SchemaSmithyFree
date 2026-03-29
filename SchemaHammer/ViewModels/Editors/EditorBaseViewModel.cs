// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using CommunityToolkit.Mvvm.ComponentModel;
using Schema.Utility;
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

    /// <summary>Strips SQL Server bracket quoting from an identifier name: [dbo].[Users] → dbo.Users</summary>
    internal static string StripBrackets(string? name)
    {
        return name?.Replace("[", "").Replace("]", "") ?? "";
    }

    /// <summary>Checks whether a domain object name (possibly bracketed) matches tree node text (unbracketed).</summary>
    internal static bool NameMatchesNodeText(string domainName, string nodeText)
    {
        return StripBrackets(domainName).Equals(nodeText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Extracts a {{TokenName}} at the given cursor position, or null if none.</summary>
    internal static string? ExtractTokenAtPosition(string text, int position)
    {
        if (string.IsNullOrEmpty(text) || position < 0 || position >= text.Length)
            return null;

        var searchFrom = Math.Min(position + 1, text.Length - 1);
        var openIdx = text.LastIndexOf("{{", searchFrom, StringComparison.Ordinal);
        if (openIdx < 0) return null;

        var closeIdx = text.IndexOf("}}", openIdx + 2, StringComparison.Ordinal);
        if (closeIdx < 0) return null;

        if (position < openIdx || position > closeIdx + 1) return null;

        var tokenName = text.Substring(openIdx + 2, closeIdx - openIdx - 2).Trim();
        return string.IsNullOrEmpty(tokenName) ? null : tokenName;
    }

    /// <summary>Navigates to the definition of a script token by finding its template or product owner.</summary>
    internal void NavigateToTokenDefinition(string tokenName)
    {
        if (NavigateToNode == null) return;

        TreeNodeModel? templateNode = null;
        TreeNodeModel? productNode = null;
        var current = Node;
        while (current != null)
        {
            if (current.Tag == "Template" && templateNode == null)
                templateNode = current;
            if (current.Tag == "Product" || current.Parent == null)
                productNode = current;
            current = current.Parent;
        }

        // Check template tokens first (they override product)
        if (templateNode != null && !string.IsNullOrEmpty(templateNode.NodePath))
        {
            try
            {
                var templateJsonPath = templateNode.NodePath.EndsWith("Template.json", StringComparison.OrdinalIgnoreCase)
                    ? templateNode.NodePath
                    : System.IO.Path.Combine(templateNode.NodePath, "Template.json");
                var template = JsonHelper.Load<Schema.Domain.Template>(templateJsonPath);
                if (template.ScriptTokens.ContainsKey(tokenName))
                {
                    PendingTokenName = tokenName;
                    NavigateToNode(templateNode);
                    return;
                }
            }
            catch { /* ignore — file may not exist */ }
        }

        // Fall back to product tokens
        if (productNode != null && !string.IsNullOrEmpty(productNode.NodePath))
        {
            try
            {
                var product = JsonHelper.ProductLoad<Schema.Domain.Product>(
                    System.IO.Path.Combine(productNode.NodePath, "Product.json"));
                if (product.ScriptTokens.ContainsKey(tokenName))
                {
                    PendingTokenName = tokenName;
                    NavigateToNode(productNode);
                    return;
                }
            }
            catch { /* ignore — file may not exist */ }
        }

        // Token not found — navigate to template if available, else product
        PendingTokenName = tokenName;
        if (templateNode != null)
            NavigateToNode(templateNode);
        else if (productNode != null)
            NavigateToNode(productNode);
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchemaHammer.Models;

public partial class TreeNodeModel : ObservableObject
{
    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private string _tag = "";

    [ObservableProperty]
    private string _imageKey = "folder";

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public string NodePath { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public TreeNodeModel? Parent { get; set; }
    public ObservableCollection<TreeNodeModel> Children { get; } = [];

    public Action? ExpandAction { get; set; }
    private bool _isLazyExpanded;

    public static Action<bool>? SetBusyCallback { get; set; }

    public string FullTreePath
    {
        get
        {
            var parts = new List<string>();
            var current = this;
            while (current != null)
            {
                parts.Insert(0, current.Text);
                current = current.Parent;
            }
            return string.Join("\\", parts);
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value) return;
        if (ExpandAction == null) return;
        if (_isLazyExpanded) return;

        var showBusy = Children.Count > 10 || !_isLazyExpanded;
        if (showBusy) SetBusyCallback?.Invoke(true);

        try
        {
            OnExpanding();
        }
        finally
        {
            if (showBusy) SetBusyCallback?.Invoke(false);
        }
    }

    private void OnExpanding()
    {
        Children.Clear();
        _isLazyExpanded = true;
        ExpandAction?.Invoke();
    }

    public void EnsureExpanded()
    {
        if (!IsExpanded)
            IsExpanded = true;
        else if (ExpandAction != null && !_isLazyExpanded)
            OnExpanding();
    }

    public List<TreeNodeModel> GetAncestorChain()
    {
        var chain = new List<TreeNodeModel>();
        var current = this;
        while (current != null)
        {
            chain.Insert(0, current);
            current = current.Parent;
        }
        return chain;
    }

    public TreeNodeModel? FindByTreePath(string treePath)
    {
        if (string.IsNullOrEmpty(treePath)) return null;

        var parts = treePath.Split('\\');
        var current = this;

        // Start from index 1 since index 0 is this node
        for (var i = 1; i < parts.Length; i++)
        {
            current.EnsureExpanded();
            var child = current.Children.FirstOrDefault(c =>
                c.Text.Equals(parts[i], StringComparison.OrdinalIgnoreCase));
            if (child == null) return null;
            current = child;
        }

        return current;
    }

    public void CollapseAllChildren()
    {
        foreach (var child in Children)
        {
            child.IsExpanded = false;
            child.CollapseAllChildren();
        }
    }
}

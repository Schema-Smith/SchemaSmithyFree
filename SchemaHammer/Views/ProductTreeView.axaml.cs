// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SchemaHammer.Models;
using SchemaHammer.ViewModels;

namespace SchemaHammer.Views;

public partial class ProductTreeView : UserControl
{
    public ProductTreeView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ProductTreeViewModel vm)
        {
            vm.NodeSelected += OnNodeSelected;
        }
    }

    private void OnNodeSelected(TreeNodeModel node, TreeNodeModel? _)
    {
        var ancestor = node.Parent;
        while (ancestor != null)
        {
            ancestor.IsExpanded = true;
            ancestor = ancestor.Parent;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var treeView = this.FindControl<TreeView>("ProductTree");
            if (treeView == null) return;

            var tvi = FindTreeViewItem(treeView, node);
            tvi?.BringIntoView();
        }, DispatcherPriority.Background);
    }

    private static TreeViewItem? FindTreeViewItem(Control parent, TreeNodeModel target)
    {
        foreach (var child in parent.GetVisualDescendants())
        {
            if (child is TreeViewItem tvi && tvi.DataContext == target)
                return tvi;
        }
        return null;
    }
}

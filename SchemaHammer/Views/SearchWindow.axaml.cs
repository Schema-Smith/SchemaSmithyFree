// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia.Controls;
using Avalonia.Input;
using SchemaHammer.Models;
using SchemaHammer.ViewModels;

namespace SchemaHammer.Views;

public partial class SearchWindow : Window
{
    public SearchWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WireDataGridDoubleClick();
    }

    private void WireDataGridDoubleClick()
    {
        var treeGrid = this.FindControl<DataGrid>("TreeResultsGrid");
        var codeGrid = this.FindControl<DataGrid>("CodeResultsGrid");

        if (treeGrid != null)
            treeGrid.DoubleTapped += OnTreeGridDoubleTapped;
        if (codeGrid != null)
            codeGrid.DoubleTapped += OnCodeGridDoubleTapped;
    }

    private void OnTreeGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SearchViewModel vm && sender is DataGrid grid && grid.SelectedItem is SearchResultItem item)
            vm.SelectTreeResultCommand.Execute(item);
    }

    private void OnCodeGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SearchViewModel vm && sender is DataGrid grid && grid.SelectedItem is SearchResultItem item)
            vm.SelectCodeResultCommand.Execute(item);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}

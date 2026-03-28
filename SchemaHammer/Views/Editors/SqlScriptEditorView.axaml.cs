// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SchemaHammer.Controls;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.Views.Editors;

public partial class SqlScriptEditorView : UserControl
{
    public SqlScriptEditorView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is SqlScriptEditorViewModel vm)
        {
            var findBar = this.FindControl<FindBarControl>("FindBar");
            if (findBar != null)
                findBar.DataContext = vm.FindBar;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not SqlScriptEditorViewModel vm) return;

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F)
        {
            vm.ShowFindBar();
            this.FindControl<FindBarControl>("FindBar")?.FocusSearchBox();
            e.Handled = true;
            return;
        }

        if (vm.IsFindBarVisible)
        {
            if (e.Key == Key.Escape)
            {
                vm.HideFindBar();
                this.FindControl<SqlEditorControl>("SqlEditor")?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.F3 && e.KeyModifiers == KeyModifiers.Shift)
            {
                vm.FindBar.FindPreviousCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                vm.FindBar.FindNextCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SchemaHammer.Controls;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;

namespace SchemaHammer.Views.Editors;

public partial class SqlScriptEditorView : UserControl
{
    private FindBarViewModel? _findBarViewModel;

    public SqlScriptEditorView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _findBarViewModel = new FindBarViewModel(new SearchService());
        var findBar = this.FindControl<FindBarControl>("FindBar");
        if (findBar != null)
            findBar.DataContext = _findBarViewModel;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F)
        {
            ShowFindBar();
            e.Handled = true;
            return;
        }

        if (_findBarViewModel is { IsVisible: true })
        {
            if (e.Key == Key.Escape)
            {
                _findBarViewModel.IsVisible = false;
                this.FindControl<SqlEditorControl>("SqlEditor")?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.F3 && e.KeyModifiers == KeyModifiers.Shift)
            {
                _findBarViewModel.FindPreviousCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                _findBarViewModel.FindNextCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void ShowFindBar()
    {
        if (_findBarViewModel == null) return;

        var editorText = (DataContext as SchemaHammer.ViewModels.Editors.SqlScriptEditorViewModel)?.DisplayContent ?? "";
        _findBarViewModel.SetEditorText(editorText);
        _findBarViewModel.IsVisible = true;

        var findBar = this.FindControl<FindBarControl>("FindBar");
        findBar?.FocusSearchBox();
    }
}

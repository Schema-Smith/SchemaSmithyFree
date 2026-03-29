// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using SchemaHammer.Highlighting;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.Controls;

public partial class SqlEditorControl : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SqlEditorControl, string>(nameof(Text), defaultValue: "");

    public static readonly StyledProperty<double> EditorHeightProperty =
        AvaloniaProperty.Register<SqlEditorControl, double>(nameof(EditorHeight), defaultValue: double.NaN);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double EditorHeight
    {
        get => GetValue(EditorHeightProperty);
        set => SetValue(EditorHeightProperty, value);
    }

    public SqlEditorControl()
    {
        InitializeComponent();

        var editor = this.FindControl<TextEditor>("Editor")!;
        editor.SyntaxHighlighting = SqlEditorSetup.GetTSqlHighlighting();
        editor.TextArea.TextView.BackgroundRenderers.Add(new TokenHighlightRenderer(editor));
        editor.TextArea.TextView.LineTransformers.Add(new TokenTextTransformer());
        editor.DoubleTapped += OnEditorDoubleTapped;
    }

    private void OnEditorDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TextEditor editor) return;

        var vm = FindEditorViewModel();
        if (vm == null) return;

        var position = editor.CaretOffset;
        var text = editor.Text;
        var tokenName = EditorBaseViewModel.ExtractTokenAtPosition(text, position);
        if (tokenName != null)
            vm.NavigateToTokenDefinition(tokenName);
    }

    private EditorBaseViewModel? FindEditorViewModel()
    {
        // Walk up the visual tree to find the nearest DataContext that is an EditorBaseViewModel
        var current = this as Visual;
        while (current != null)
        {
            if (current is Control { DataContext: EditorBaseViewModel vm })
                return vm;
            current = current.GetVisualParent();
        }
        return null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            var editor = this.FindControl<TextEditor>("Editor");
            if (editor != null)
                editor.Text = change.GetNewValue<string>() ?? "";
        }

        if (change.Property == EditorHeightProperty)
        {
            var editor = this.FindControl<TextEditor>("Editor");
            if (editor != null && change.GetNewValue<double>() is var h && !double.IsNaN(h))
                editor.Height = h;
        }
    }
}

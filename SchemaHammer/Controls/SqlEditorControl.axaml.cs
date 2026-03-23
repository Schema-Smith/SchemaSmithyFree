// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using SchemaHammer.Highlighting;

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

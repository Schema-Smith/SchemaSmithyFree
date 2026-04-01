// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia.Controls;

namespace SchemaHammer.Controls;

public partial class FindBarControl : UserControl
{
    public FindBarControl()
    {
        InitializeComponent();
    }

    public void FocusSearchBox()
    {
        var textBox = this.FindControl<TextBox>("FindTextBox");
        textBox?.Focus();
    }
}

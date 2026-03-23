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

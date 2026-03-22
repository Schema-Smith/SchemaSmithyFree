using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class PlaceholderEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Node?.Text ?? "No Selection";
    public string NodeTag => Node?.Tag ?? "";
    public string NodePath => Node?.NodePath ?? "";
    public string TemplateName => Node?.TemplateName ?? "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        OnPropertyChanged(nameof(NodeTag));
        OnPropertyChanged(nameof(NodePath));
        OnPropertyChanged(nameof(TemplateName));
    }
}

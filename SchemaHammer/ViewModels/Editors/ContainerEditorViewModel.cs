using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class ContainerEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Node?.Text ?? "Container";

    public string ContainerName => Node?.Text ?? "";
    public string ParentContext => BuildParentContext();

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        OnPropertyChanged(nameof(ContainerName));
        OnPropertyChanged(nameof(ParentContext));
    }

    private string BuildParentContext()
    {
        if (Node?.Parent == null) return "";
        var parts = new List<string>();
        var current = Node.Parent;
        while (current != null)
        {
            parts.Insert(0, current.Text);
            current = current.Parent;
        }
        return string.Join(" / ", parts);
    }
}

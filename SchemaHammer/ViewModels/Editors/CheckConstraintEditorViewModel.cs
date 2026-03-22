using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class CheckConstraintEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;
    public string Name { get; private set; } = "";
    public string Expression { get; private set; } = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        var table = ColumnEditorViewModel.FindParentTable(node);
        var cc = table?.CheckConstraints.FirstOrDefault(c => c.Name == node.Text);
        if (cc != null) { Name = cc.Name; Expression = cc.Expression ?? ""; }
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Expression));
    }
}

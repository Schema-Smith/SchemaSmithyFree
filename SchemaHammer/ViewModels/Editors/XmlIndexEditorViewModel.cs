using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class XmlIndexEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;
    public string Name { get; private set; } = "";
    public bool IsPrimary { get; private set; }
    public string Column { get; private set; } = "";
    public string PrimaryIndex { get; private set; } = "";
    public string SecondaryIndexType { get; private set; } = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        var table = ColumnEditorViewModel.FindParentTable(node);
        var xi = table?.XmlIndexes.FirstOrDefault(x => NameMatchesNodeText(x.Name, node.Text));
        if (xi != null)
        {
            Name = StripBrackets(xi.Name);
            IsPrimary = xi.IsPrimary;
            Column = xi.Column ?? "";
            PrimaryIndex = xi.PrimaryIndex ?? "";
            SecondaryIndexType = xi.SecondaryIndexType ?? "";
        }
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsPrimary));
        OnPropertyChanged(nameof(Column));
        OnPropertyChanged(nameof(PrimaryIndex));
        OnPropertyChanged(nameof(SecondaryIndexType));
    }
}

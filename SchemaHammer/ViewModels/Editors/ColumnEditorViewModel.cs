using Schema.Domain;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class ColumnEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;

    public string Name { get; private set; } = "";
    public string DataType { get; private set; } = "";
    public bool Nullable { get; private set; }
    public string Default { get; private set; } = "";
    public string CheckExpression { get; private set; } = "";
    public string ComputedExpression { get; private set; } = "";
    public bool Persisted { get; private set; }
    public bool Sparse { get; private set; }
    public string Collation { get; private set; } = "";
    public string DataMaskFunction { get; private set; } = "";
    public string OldName { get; private set; } = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        LoadColumn(node);
        NotifyAllProperties();
    }

    private void LoadColumn(TreeNodeModel node)
    {
        var table = FindParentTable(node);
        if (table == null) return;

        var column = table.Columns.FirstOrDefault(c => NameMatchesNodeText(c.Name, node.Text));
        if (column == null) return;

        Name = StripBrackets(column.Name);
        DataType = column.DataType;
        Nullable = column.Nullable;
        Default = column.Default ?? "";
        CheckExpression = column.CheckExpression ?? "";
        ComputedExpression = column.ComputedExpression ?? "";
        Persisted = column.Persisted;
        Sparse = column.Sparse;
        Collation = column.Collation ?? "";
        DataMaskFunction = column.DataMaskFunction ?? "";
        OldName = column.OldName ?? "";
    }

    internal static Table? FindParentTable(TreeNodeModel node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is TableNodeModel tableNode)
                return tableNode.TableData;
            current = current.Parent;
        }
        return null;
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DataType));
        OnPropertyChanged(nameof(Nullable));
        OnPropertyChanged(nameof(Default));
        OnPropertyChanged(nameof(CheckExpression));
        OnPropertyChanged(nameof(ComputedExpression));
        OnPropertyChanged(nameof(Persisted));
        OnPropertyChanged(nameof(Sparse));
        OnPropertyChanged(nameof(Collation));
        OnPropertyChanged(nameof(DataMaskFunction));
        OnPropertyChanged(nameof(OldName));
    }
}

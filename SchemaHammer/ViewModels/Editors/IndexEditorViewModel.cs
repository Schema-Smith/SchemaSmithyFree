using Schema.Domain;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class IndexEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;

    public string Name { get; private set; } = "";
    public string CompressionType { get; private set; } = "";
    public bool PrimaryKey { get; private set; }
    public bool Unique { get; private set; }
    public bool UniqueConstraint { get; private set; }
    public bool Clustered { get; private set; }
    public bool ColumnStore { get; private set; }
    public byte FillFactor { get; private set; }
    public string IndexColumns { get; private set; } = "";
    public string IncludeColumns { get; private set; } = "";
    public string FilterExpression { get; private set; } = "";
    public bool UpdateFillFactor { get; private set; }

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        LoadIndex(node);
        NotifyAllProperties();
    }

    private void LoadIndex(TreeNodeModel node)
    {
        var index = FindIndex(node);
        if (index == null) return;

        Name = StripBrackets(index.Name);
        CompressionType = index.CompressionType ?? "NONE";
        PrimaryKey = index.PrimaryKey;
        Unique = index.Unique;
        UniqueConstraint = index.UniqueConstraint;
        Clustered = index.Clustered;
        ColumnStore = index.ColumnStore;
        FillFactor = index.FillFactor;
        IndexColumns = index.IndexColumns ?? "";
        IncludeColumns = index.IncludeColumns ?? "";
        FilterExpression = index.FilterExpression ?? "";
        UpdateFillFactor = index.UpdateFillFactor;
    }

    private static Schema.Domain.Index? FindIndex(TreeNodeModel node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is TableNodeModel tableNode)
                return tableNode.TableData?.Indexes.FirstOrDefault(i => NameMatchesNodeText(i.Name, node.Text));
            if (current is IndexedViewNodeModel ivNode)
                return ivNode.IndexedViewData?.Indexes.FirstOrDefault(i => NameMatchesNodeText(i.Name, node.Text));
            current = current.Parent;
        }
        return null;
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(CompressionType));
        OnPropertyChanged(nameof(PrimaryKey));
        OnPropertyChanged(nameof(Unique));
        OnPropertyChanged(nameof(UniqueConstraint));
        OnPropertyChanged(nameof(Clustered));
        OnPropertyChanged(nameof(ColumnStore));
        OnPropertyChanged(nameof(FillFactor));
        OnPropertyChanged(nameof(IndexColumns));
        OnPropertyChanged(nameof(IncludeColumns));
        OnPropertyChanged(nameof(FilterExpression));
        OnPropertyChanged(nameof(UpdateFillFactor));
    }
}

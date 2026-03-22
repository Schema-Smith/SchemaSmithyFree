using System.Collections.ObjectModel;
using Schema.Domain;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class TableEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => $"{Schema}.{Name}";

    public string Schema { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string CompressionType { get; private set; } = "";
    public bool IsTemporal { get; private set; }
    public bool UpdateFillFactor { get; private set; }
    public string OldName { get; private set; } = "";

    public ObservableCollection<string> ColumnSummary { get; } = [];
    public ObservableCollection<string> IndexSummary { get; } = [];
    public ObservableCollection<string> ForeignKeySummary { get; } = [];
    public ObservableCollection<string> CheckConstraintSummary { get; } = [];
    public ObservableCollection<string> StatisticSummary { get; } = [];
    public ObservableCollection<string> XmlIndexSummary { get; } = [];
    public string FullTextIndexSummary { get; private set; } = "None";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        var tableNode = node as TableNodeModel;
        var table = tableNode?.TableData;
        if (table == null) return;

        Schema = table.Schema ?? "dbo";
        Name = table.Name ?? "";
        CompressionType = table.CompressionType ?? "NONE";
        IsTemporal = table.IsTemporal;
        UpdateFillFactor = table.UpdateFillFactor;
        OldName = table.OldName ?? "";

        LoadSummaryLists(table);
        NotifyAllProperties();
    }

    private void LoadSummaryLists(Table table)
    {
        ColumnSummary.Clear();
        foreach (var c in table.Columns) ColumnSummary.Add($"{c.Name} ({c.DataType})");

        IndexSummary.Clear();
        foreach (var i in table.Indexes) IndexSummary.Add(i.Name);

        ForeignKeySummary.Clear();
        foreach (var fk in table.ForeignKeys) ForeignKeySummary.Add(fk.Name);

        CheckConstraintSummary.Clear();
        foreach (var cc in table.CheckConstraints) CheckConstraintSummary.Add(cc.Name);

        StatisticSummary.Clear();
        foreach (var s in table.Statistics) StatisticSummary.Add(s.Name);

        XmlIndexSummary.Clear();
        foreach (var xi in table.XmlIndexes) XmlIndexSummary.Add(xi.Name);

        FullTextIndexSummary = table.FullTextIndex != null
            ? $"Catalog: {table.FullTextIndex.FullTextCatalog}, Key: {table.FullTextIndex.KeyIndex}"
            : "None";
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Schema));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(CompressionType));
        OnPropertyChanged(nameof(IsTemporal));
        OnPropertyChanged(nameof(UpdateFillFactor));
        OnPropertyChanged(nameof(OldName));
        OnPropertyChanged(nameof(FullTextIndexSummary));
    }
}

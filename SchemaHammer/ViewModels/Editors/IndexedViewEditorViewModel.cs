using System.Collections.ObjectModel;
using Schema.Domain;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class IndexedViewEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => $"{Schema}.{Name}";

    public string Schema { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Definition { get; private set; } = "";
    public ObservableCollection<string> IndexSummary { get; } = [];

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        var ivNode = node as IndexedViewNodeModel;
        var iv = ivNode?.IndexedViewData;
        if (iv == null) return;

        Schema = iv.Schema ?? "dbo";
        Name = iv.Name ?? "";
        Definition = iv.Definition ?? "";

        IndexSummary.Clear();
        foreach (var idx in iv.Indexes) IndexSummary.Add(idx.Name);

        OnPropertyChanged(nameof(Schema));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Definition));
    }
}

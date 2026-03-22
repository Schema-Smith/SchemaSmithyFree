using Schema.Domain;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class TableEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Node?.Text ?? "Table";

    public string CompressionType { get; private set; } = "";
    public bool IsTemporal { get; private set; }
    public bool UpdateFillFactor { get; private set; }
    public string OldName { get; private set; } = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        var tableNode = node as TableNodeModel;
        var table = tableNode?.TableData;
        if (table == null) return;

        CompressionType = table.CompressionType ?? "NONE";
        IsTemporal = table.IsTemporal;
        UpdateFillFactor = table.UpdateFillFactor;
        OldName = table.OldName ?? "";

        NotifyAllProperties();
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(CompressionType));
        OnPropertyChanged(nameof(IsTemporal));
        OnPropertyChanged(nameof(UpdateFillFactor));
        OnPropertyChanged(nameof(OldName));
    }
}

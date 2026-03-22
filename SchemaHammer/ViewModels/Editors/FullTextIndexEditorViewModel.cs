using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class FullTextIndexEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => "Full Text Index";
    public string FullTextCatalog { get; private set; } = "";
    public string KeyIndex { get; private set; } = "";
    public string ChangeTracking { get; private set; } = "";
    public string StopList { get; private set; } = "";
    public string Columns { get; private set; } = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        // FullTextIndex is a direct child of TableNodeModel, not inside a container
        var tableNode = node.Parent as TableNodeModel;
        var fti = tableNode?.TableData?.FullTextIndex;
        if (fti != null)
        {
            FullTextCatalog = fti.FullTextCatalog ?? "";
            KeyIndex = fti.KeyIndex ?? "";
            ChangeTracking = fti.ChangeTracking ?? "";
            StopList = fti.StopList ?? "";
            Columns = fti.Columns ?? "";
        }
        OnPropertyChanged(nameof(FullTextCatalog));
        OnPropertyChanged(nameof(KeyIndex));
        OnPropertyChanged(nameof(ChangeTracking));
        OnPropertyChanged(nameof(StopList));
        OnPropertyChanged(nameof(Columns));
    }
}

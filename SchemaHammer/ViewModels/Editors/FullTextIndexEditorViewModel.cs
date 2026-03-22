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
        var table = ColumnEditorViewModel.FindParentTable(node);
        var fti = table?.FullTextIndex;
        if (fti != null)
        {
            FullTextCatalog = StripBrackets(fti.FullTextCatalog);
            KeyIndex = StripBrackets(fti.KeyIndex);
            ChangeTracking = fti.ChangeTracking ?? "";
            StopList = fti.StopList ?? "";
            Columns = StripBrackets(fti.Columns);
        }
        OnPropertyChanged(nameof(FullTextCatalog));
        OnPropertyChanged(nameof(KeyIndex));
        OnPropertyChanged(nameof(ChangeTracking));
        OnPropertyChanged(nameof(StopList));
        OnPropertyChanged(nameof(Columns));
    }
}

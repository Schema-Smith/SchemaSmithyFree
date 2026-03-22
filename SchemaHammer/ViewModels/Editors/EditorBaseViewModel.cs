using CommunityToolkit.Mvvm.ComponentModel;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public abstract partial class EditorBaseViewModel : ObservableObject
{
    public abstract string EditorTitle { get; }
    public TreeNodeModel? Node { get; protected set; }

    [ObservableProperty]
    private string _editorLabel = "";

    public virtual void ChangeNode(TreeNodeModel node)
    {
        Node = node;
        EditorLabel = node.Text;
        OnPropertyChanged(nameof(EditorTitle));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public abstract class EditorBaseViewModel : ObservableObject
{
    public abstract string EditorTitle { get; }
    public TreeNodeModel? Node { get; protected set; }

    public virtual void ChangeNode(TreeNodeModel node)
    {
        Node = node;
        OnPropertyChanged(nameof(EditorTitle));
    }
}

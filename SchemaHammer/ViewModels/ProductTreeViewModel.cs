using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels;

public partial class ProductTreeViewModel : ObservableObject
{
    public ObservableCollection<TreeNodeModel> RootNodes { get; } = [];

    [ObservableProperty]
    private TreeNodeModel? _selectedNode;

    public event Action<TreeNodeModel, TreeNodeModel?>? NodeSelected;

    partial void OnSelectedNodeChanged(TreeNodeModel? oldValue, TreeNodeModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null)
        {
            newValue.IsSelected = true;
            NodeSelected?.Invoke(newValue, oldValue);
        }
    }

    public void SetRootNodes(List<TreeNodeModel> nodes)
    {
        RootNodes.Clear();
        foreach (var node in nodes)
            RootNodes.Add(node);
    }
}

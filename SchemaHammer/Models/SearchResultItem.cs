using CommunityToolkit.Mvvm.ComponentModel;

namespace SchemaHammer.Models;

public partial class SearchResultItem : ObservableObject
{
    public string Name { get; init; } = "";
    public string Template { get; init; } = "";
    public string Type { get; init; } = "";
    public TreeNodeModel? Node { get; init; }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchemaHammer.Models;
using SchemaHammer.Services;

namespace SchemaHammer.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IProductTreeService _treeService;

    [ObservableProperty] private string _treeSearchTerm = "";
    [ObservableProperty] private string _codeSearchTerm = "";
    [ObservableProperty] private string _selectedSearchType = "Contains";
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isSearching;

    public ObservableCollection<SearchResultItem> TreeSearchResults { get; } = [];
    public ObservableCollection<SearchResultItem> CodeSearchResults { get; } = [];
    public ObservableCollection<string> SearchTypes { get; } = ["Contains", "Begins With", "Ends With"];

    [ObservableProperty] private TreeNodeModel? _selectedResultNode;

    private CancellationTokenSource? _debounceTokenSource;

    public SearchViewModel(IProductTreeService treeService, string defaultTab = "Tree")
    {
        _treeService = treeService;
        SelectedTabIndex = defaultTab == "Code" ? 1 : 0;
    }

    partial void OnTreeSearchTermChanged(string value)
    {
        _ = DebouncedTreeSearch();
    }

    private async Task DebouncedTreeSearch()
    {
        _debounceTokenSource?.Cancel();
        _debounceTokenSource = new CancellationTokenSource();
        var token = _debounceTokenSource.Token;

        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested)
                SearchTree();
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled — new keystroke arrived
        }
    }

    [RelayCommand]
    private void SearchTree()
    {
        TreeSearchResults.Clear();
        if (string.IsNullOrWhiteSpace(TreeSearchTerm)) return;

        var term = TreeSearchTerm.Trim();

        foreach (var node in _treeService.SearchList)
        {
            if (IsContainerNode(node)) continue;
            if (!MatchesSearchType(node.Text, term)) continue;

            TreeSearchResults.Add(new SearchResultItem
            {
                Name = node.Text,
                Template = node.TemplateName,
                Type = node.Tag,
                Node = node
            });
        }
    }

    private bool MatchesSearchType(string text, string term)
    {
        return SelectedSearchType switch
        {
            "Begins With" => text.StartsWith(term, StringComparison.OrdinalIgnoreCase),
            "Ends With" => text.EndsWith(term, StringComparison.OrdinalIgnoreCase),
            _ => text.Contains(term, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool IsContainerNode(TreeNodeModel node)
    {
        var tag = node.Tag;
        return tag.EndsWith("Container", StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("Folder", StringComparison.OrdinalIgnoreCase)
            || tag is "Templates" or "Tables" or "Indexed Views";
    }

    [RelayCommand]
    private void SelectTreeResult(SearchResultItem? item)
    {
        if (item?.Node != null)
            SelectedResultNode = item.Node;
    }

    [RelayCommand]
    private void SelectCodeResult(SearchResultItem? item)
    {
        // Placeholder — code search implemented in Task 4
    }

    [RelayCommand]
    private Task SearchCode()
    {
        // Placeholder — code search implemented in Task 4
        return Task.CompletedTask;
    }
}

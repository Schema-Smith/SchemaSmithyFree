// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schema.Isolators;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;

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

    partial void OnSelectedSearchTypeChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(TreeSearchTerm))
            SearchTree();
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
        if (item?.Node == null) return;

        if (item.Type == "Script Token")
        {
            var tokenName = item.Name.Replace("{{", "").Replace("}}", "").Trim();
            EditorBaseViewModel.PendingTokenName = tokenName;
        }

        SelectedResultNode = item.Node;
    }

    [RelayCommand]
    private Task SearchCode()
    {
        CodeSearchResults.Clear();
        if (string.IsNullOrWhiteSpace(CodeSearchTerm)) return Task.CompletedTask;

        var term = CodeSearchTerm.Trim();
        IsSearching = true;

        try
        {
            EnsureTablesExpanded();
            SearchSqlScripts(term);
            SearchTableMetadata(term);
            SearchScriptTokens(term);
        }
        finally
        {
            IsSearching = false;
        }

        return Task.CompletedTask;
    }

    private void EnsureTablesExpanded()
    {
        foreach (var node in _treeService.SearchList.ToList())
        {
            if (node.Tag is "Tables" or "Indexed Views")
                node.EnsureExpanded();
        }
    }

    private void SearchSqlScripts(string term)
    {
        foreach (var node in _treeService.SearchList)
        {
            if (node.Tag != "Sql Script") continue;
            if (string.IsNullOrEmpty(node.NodePath)) continue;

            try
            {
                var content = ProductFileWrapper.GetFromFactory().ReadAllText(node.NodePath);
                if (content.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = node.Text,
                        Template = string.IsNullOrEmpty(node.TemplateName) ? "(Product)" : node.TemplateName,
                        Type = "Sql Script",
                        Node = node
                    });
                }
            }
            catch
            {
                // Skip unreadable files
            }
        }
    }

    private void SearchTableMetadata(string term)
    {
        foreach (var node in _treeService.SearchList)
        {
            if (node is not TableNodeModel tableNode || tableNode.TableData == null) continue;
            var table = tableNode.TableData;
            var template = tableNode.TemplateName;

            // Table name
            if (ContainsIgnoreCase($"{table.Schema}.{table.Name}", term))
            {
                CodeSearchResults.Add(new SearchResultItem
                {
                    Name = $"{table.Schema}.{table.Name}",
                    Template = template,
                    Type = "Table",
                    Node = tableNode
                });
            }

            // Columns
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var col = table.Columns[i];
                if (ContainsInAny(term, col.Name, col.Default, col.CheckExpression, col.ComputedExpression))
                {
                    var colNode = i < tableNode.ColumnNodes.Length ? tableNode.ColumnNodes[i] : tableNode;
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = EditorBaseViewModel.StripBrackets(col.Name),
                        Template = template,
                        Type = "Column",
                        Node = colNode
                    });
                }
            }

            // Indexes
            for (var i = 0; i < table.Indexes.Count; i++)
            {
                var idx = table.Indexes[i];
                if (ContainsInAny(term, idx.Name, idx.IndexColumns, idx.IncludeColumns, idx.FilterExpression))
                {
                    var idxNode = i < tableNode.IndexNodes.Length ? tableNode.IndexNodes[i] : tableNode;
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = EditorBaseViewModel.StripBrackets(idx.Name),
                        Template = template,
                        Type = "Index",
                        Node = idxNode
                    });
                }
            }

            // Foreign Keys
            for (var i = 0; i < table.ForeignKeys.Count; i++)
            {
                var fk = table.ForeignKeys[i];
                if (ContainsInAny(term, fk.Name, fk.Columns, fk.RelatedTable, fk.RelatedColumns))
                {
                    var fkNode = i < tableNode.ForeignKeyNodes.Length ? tableNode.ForeignKeyNodes[i] : tableNode;
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = EditorBaseViewModel.StripBrackets(fk.Name),
                        Template = template,
                        Type = "Foreign Key",
                        Node = fkNode
                    });
                }
            }

            // Check Constraints
            for (var i = 0; i < table.CheckConstraints.Count; i++)
            {
                var cc = table.CheckConstraints[i];
                if (ContainsInAny(term, cc.Name, cc.Expression))
                {
                    var ccNode = i < tableNode.CheckConstraintNodes.Length ? tableNode.CheckConstraintNodes[i] : tableNode;
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = EditorBaseViewModel.StripBrackets(cc.Name),
                        Template = template,
                        Type = "Check Constraint",
                        Node = ccNode
                    });
                }
            }

            // Statistics
            for (var i = 0; i < table.Statistics.Count; i++)
            {
                var stat = table.Statistics[i];
                if (ContainsInAny(term, stat.Name, stat.Columns, stat.FilterExpression))
                {
                    var statNode = i < tableNode.StatisticNodes.Length ? tableNode.StatisticNodes[i] : tableNode;
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = EditorBaseViewModel.StripBrackets(stat.Name),
                        Template = template,
                        Type = "Statistic",
                        Node = statNode
                    });
                }
            }
        }
    }

    private void SearchScriptTokens(string term)
    {
        // Product-level tokens
        if (_treeService.Product != null)
        {
            var productNode = _treeService.SearchList
                .FirstOrDefault(n => n.Tag == "Product");

            foreach (var token in _treeService.Product.ScriptTokens)
            {
                if (ContainsInAny(term, token.Key, token.Value))
                {
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = "{{" + token.Key + "}}",
                        Template = "(Product)",
                        Type = "Script Token",
                        Node = productNode
                    });
                }
            }
        }

        // Template-level tokens
        foreach (var (templateName, template) in _treeService.Templates)
        {
            var templateNode = _treeService.SearchList
                .FirstOrDefault(n => n.Tag == "Template" && n.Text == templateName);

            foreach (var token in template.ScriptTokens)
            {
                if (ContainsInAny(term, token.Key, token.Value))
                {
                    CodeSearchResults.Add(new SearchResultItem
                    {
                        Name = "{{" + token.Key + "}}",
                        Template = templateName,
                        Type = "Script Token",
                        Node = templateNode
                    });
                }
            }
        }
    }

    private static bool ContainsIgnoreCase(string? text, string term)
    {
        return text != null && text.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsInAny(string term, params string?[] values)
    {
        return values.Any(v => v != null && v.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;

namespace SchemaHammer.Services;

public class NavigationService : INavigationService
{
    private const int MaxHistory = 20;
    private readonly List<TreeNodeModel> _history = [];

    public bool CanGoBack => _history.Count > 0;
    public IReadOnlyList<TreeNodeModel> History => _history;

    public void Push(TreeNodeModel node)
    {
        _history.Add(node);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);
    }

    public TreeNodeModel? Pop()
    {
        if (_history.Count == 0) return null;
        var node = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        return node;
    }

    public TreeNodeModel? NavigateTo(int index)
    {
        if (index < 0 || index >= _history.Count) return null;
        var node = _history[index];
        _history.RemoveRange(index, _history.Count - index);
        return node;
    }

    public void Clear()
    {
        _history.Clear();
    }
}

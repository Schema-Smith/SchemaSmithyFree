// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchemaHammer.Services;

namespace SchemaHammer.ViewModels;

public partial class FindBarViewModel : ObservableObject
{
    private readonly ISearchService _searchService;
    private string _editorText = "";

    [ObservableProperty] private string _searchTerm = "";
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _matchCase;
    [ObservableProperty] private int _matchCount;
    [ObservableProperty] private int _currentMatchIndex;
    [ObservableProperty] private int _selectionStart;
    [ObservableProperty] private int _selectionLength;

    public FindBarViewModel(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public void SetEditorText(string text)
    {
        _editorText = text;
        OnSearchTermOrCaseChanged();
    }

    partial void OnSearchTermChanged(string value)
    {
        OnSearchTermOrCaseChanged();
    }

    public void OnSearchTermOrCaseChanged()
    {
        MatchCount = _searchService.CountMatches(_editorText, SearchTerm, MatchCase);
        CurrentMatchIndex = 0;
        SelectionStart = 0;
        SelectionLength = 0;
    }

    [RelayCommand]
    private void FindNext()
    {
        if (string.IsNullOrEmpty(SearchTerm)) return;

        var startOffset = SelectionStart + SelectionLength;
        var result = _searchService.FindNext(_editorText, SearchTerm, startOffset, MatchCase);
        if (result == null) return;

        SelectionStart = result.Value.Start;
        SelectionLength = result.Value.Length;
        UpdateCurrentMatchIndex();
    }

    [RelayCommand]
    private void FindPrevious()
    {
        if (string.IsNullOrEmpty(SearchTerm)) return;

        var startOffset = Math.Max(0, SelectionStart - 1);
        var result = _searchService.FindPrevious(_editorText, SearchTerm, startOffset, MatchCase);
        if (result == null) return;

        SelectionStart = result.Value.Start;
        SelectionLength = result.Value.Length;
        UpdateCurrentMatchIndex();
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
    }

    private void UpdateCurrentMatchIndex()
    {
        if (MatchCount == 0) { CurrentMatchIndex = 0; return; }

        var count = 0;
        var index = 0;
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        while ((index = _editorText.IndexOf(SearchTerm, index, comparison)) >= 0)
        {
            count++;
            if (index >= SelectionStart) break;
            index += SearchTerm.Length;
        }
        CurrentMatchIndex = count;
    }
}

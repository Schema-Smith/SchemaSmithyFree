// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaHammer.Services;

public class SearchService : ISearchService
{
    public (int Start, int Length)? FindNext(string text, string term, int startOffset, bool matchCase)
    {
        if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text))
            return null;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Search from offset to end
        var index = text.IndexOf(term, startOffset, comparison);
        if (index >= 0)
            return (index, term.Length);

        // Wrap around: search from beginning to offset
        if (startOffset > 0)
        {
            index = text.IndexOf(term, 0, comparison);
            if (index >= 0)
                return (index, term.Length);
        }

        return null;
    }

    public (int Start, int Length)? FindPrevious(string text, string term, int startOffset, bool matchCase)
    {
        if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text))
            return null;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Search backwards from offset
        var searchEnd = Math.Min(startOffset, text.Length);
        var index = text.LastIndexOf(term, searchEnd, comparison);
        if (index >= 0)
            return (index, term.Length);

        // Wrap around: search from end
        index = text.LastIndexOf(term, text.Length - 1, comparison);
        if (index >= 0)
            return (index, term.Length);

        return null;
    }

    public int CountMatches(string text, string term, bool matchCase)
    {
        if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text))
            return 0;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(term, index, comparison)) >= 0)
        {
            count++;
            index += term.Length;
        }

        return count;
    }
}

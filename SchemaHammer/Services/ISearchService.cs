namespace SchemaHammer.Services;

public interface ISearchService
{
    (int Start, int Length)? FindNext(string text, string term, int startOffset, bool matchCase);
    (int Start, int Length)? FindPrevious(string text, string term, int startOffset, bool matchCase);
    int CountMatches(string text, string term, bool matchCase);
}

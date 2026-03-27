// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Services;
using SchemaHammer.ViewModels;

namespace SchemaHammer.UnitTests;

public class FindBarViewModelTests
{
    [Test]
    public void FindNext_FindsFirstMatch()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello world hello");
        vm.SearchTerm = "hello";

        vm.FindNextCommand.Execute(null);

        Assert.That(vm.SelectionStart, Is.EqualTo(0));
        Assert.That(vm.SelectionLength, Is.EqualTo(5));
    }

    [Test]
    public void FindNext_AdvancesToSecondMatch()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello world hello");
        vm.SearchTerm = "hello";

        vm.FindNextCommand.Execute(null);
        vm.FindNextCommand.Execute(null);

        Assert.That(vm.SelectionStart, Is.EqualTo(12));
    }

    [Test]
    public void FindPrevious_WrapsToLastMatch()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello world hello");
        vm.SearchTerm = "hello";

        vm.FindPreviousCommand.Execute(null);

        Assert.That(vm.SelectionStart, Is.EqualTo(12));
    }

    [Test]
    public void MatchCount_UpdatesOnSearchTermChange()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("abc abc abc");
        vm.SearchTerm = "abc";

        Assert.That(vm.MatchCount, Is.EqualTo(3));
    }

    [Test]
    public void MatchCount_ZeroWhenNoMatch()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello world");
        vm.SearchTerm = "xyz";

        Assert.That(vm.MatchCount, Is.EqualTo(0));
    }

    [Test]
    public void CurrentMatchIndex_UpdatesOnFind()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("abc abc abc");
        vm.SearchTerm = "abc";

        vm.FindNextCommand.Execute(null);
        Assert.That(vm.CurrentMatchIndex, Is.EqualTo(1));

        vm.FindNextCommand.Execute(null);
        Assert.That(vm.CurrentMatchIndex, Is.EqualTo(2));
    }

    [Test]
    public void Close_SetsIsVisibleFalse()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.IsVisible = true;

        vm.CloseCommand.Execute(null);

        Assert.That(vm.IsVisible, Is.False);
    }

    [Test]
    public void MatchCase_ChangesMatchCount()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("Hello hello HELLO");

        vm.MatchCase = false;
        vm.SearchTerm = "hello";
        Assert.That(vm.MatchCount, Is.EqualTo(3));

        vm.MatchCase = true;
        vm.SearchTerm = "hello"; // Re-trigger
        vm.OnSearchTermOrCaseChanged();
        Assert.That(vm.MatchCount, Is.EqualTo(1));
    }

    [Test]
    public void EmptySearchTerm_ZeroMatches()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello");
        vm.SearchTerm = "";
        Assert.That(vm.MatchCount, Is.EqualTo(0));
    }

    [Test]
    public void FindNext_NoMatch_DoesNotChangeSelection()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello world");
        vm.SearchTerm = "xyz";

        vm.FindNextCommand.Execute(null);

        Assert.That(vm.SelectionStart, Is.EqualTo(0));
        Assert.That(vm.SelectionLength, Is.EqualTo(0));
    }

    [Test]
    public void FindPrevious_NoMatch_DoesNotChangeSelection()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello world");
        vm.SearchTerm = "xyz";

        vm.FindPreviousCommand.Execute(null);

        Assert.That(vm.SelectionStart, Is.EqualTo(0));
        Assert.That(vm.SelectionLength, Is.EqualTo(0));
    }

    [Test]
    public void FindNext_EmptyTerm_DoesNothing()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello");
        vm.SearchTerm = "";

        vm.FindNextCommand.Execute(null);

        Assert.That(vm.SelectionStart, Is.EqualTo(0));
    }

    [Test]
    public void FindPrevious_EmptyTerm_DoesNothing()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello");
        vm.SearchTerm = "";

        vm.FindPreviousCommand.Execute(null);

        Assert.That(vm.SelectionStart, Is.EqualTo(0));
    }

    [Test]
    public void CurrentMatchIndex_ZeroWhenNoMatches()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("hello");
        vm.SearchTerm = "xyz";

        vm.FindNextCommand.Execute(null);

        Assert.That(vm.CurrentMatchIndex, Is.EqualTo(0));
    }

    [Test]
    public void UpdateCurrentMatchIndex_MatchBeforeSelectionPosition_CountsCorrectly()
    {
        // "abc abc abc" — after two FindNext calls the selection is on the second match.
        // UpdateCurrentMatchIndex must walk past the first match (index += SearchTerm.Length branch)
        // before finding the match at or after SelectionStart.
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("abc abc abc");
        vm.SearchTerm = "abc";

        vm.FindNextCommand.Execute(null); // CurrentMatchIndex == 1 (first match at 0)
        vm.FindNextCommand.Execute(null); // CurrentMatchIndex == 2 (second match at 4)

        Assert.That(vm.CurrentMatchIndex, Is.EqualTo(2));
    }

    [Test]
    public void UpdateCurrentMatchIndex_ThirdMatchOutOfThree_ReturnsThree()
    {
        // Exercises the inner loop walking past two prior matches before finding the third.
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("aa aa aa");
        vm.SearchTerm = "aa";

        vm.FindNextCommand.Execute(null); // match 1 at 0
        vm.FindNextCommand.Execute(null); // match 2 at 3
        vm.FindNextCommand.Execute(null); // match 3 at 6

        Assert.That(vm.CurrentMatchIndex, Is.EqualTo(3));
    }

    [Test]
    public void UpdateCurrentMatchIndex_MatchCase_WalksMultipleMatches()
    {
        var vm = new FindBarViewModel(new SearchService());
        vm.SetEditorText("XX XX XX");
        vm.MatchCase = true;
        vm.SearchTerm = "XX";

        vm.FindNextCommand.Execute(null); // match 1
        vm.FindNextCommand.Execute(null); // match 2 — loops past first

        Assert.That(vm.CurrentMatchIndex, Is.EqualTo(2));
    }
}

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
}

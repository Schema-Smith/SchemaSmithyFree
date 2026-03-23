// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class EditorBaseViewModelTests
{
    // Minimal concrete subclass for testing the abstract base
    private sealed class ConcreteEditorViewModel : EditorBaseViewModel
    {
        public override string EditorTitle => Node?.Text ?? "";
    }

    [Test]
    public void StripBrackets_RemovesBrackets()
    {
        // Access internal static via the concrete subclass defined in this assembly
        // InternalsVisibleTo allows direct call since both are in the test-visible scope
        var result = EditorBaseViewModel.StripBrackets("[Name]");
        Assert.That(result, Is.EqualTo("Name"));
    }

    [Test]
    public void StripBrackets_LeavesUnbracketed()
    {
        var result = EditorBaseViewModel.StripBrackets("Name");
        Assert.That(result, Is.EqualTo("Name"));
    }

    [Test]
    public void StripBrackets_HandlesNull()
    {
        var result = EditorBaseViewModel.StripBrackets(null);
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void StripBrackets_HandlesEmpty()
    {
        var result = EditorBaseViewModel.StripBrackets("");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void NameMatchesNodeText_MatchesBracketed()
    {
        var result = EditorBaseViewModel.NameMatchesNodeText("[PK_T]", "PK_T");
        Assert.That(result, Is.True);
    }

    [Test]
    public void NameMatchesNodeText_MatchesUnbracketed()
    {
        var result = EditorBaseViewModel.NameMatchesNodeText("PK_T", "PK_T");
        Assert.That(result, Is.True);
    }

    [Test]
    public void NameMatchesNodeText_CaseInsensitive()
    {
        var result = EditorBaseViewModel.NameMatchesNodeText("[pk_t]", "PK_T");
        Assert.That(result, Is.True);
    }

    [Test]
    public void NameMatchesNodeText_NoMatch()
    {
        var result = EditorBaseViewModel.NameMatchesNodeText("[Other]", "PK_T");
        Assert.That(result, Is.False);
    }

    [Test]
    public void ChangeNode_SetsNodeAndEditorLabel()
    {
        var vm = new ConcreteEditorViewModel();
        var node = new TreeNodeModel { Text = "MyNode", Tag = "SomeTag" };

        vm.ChangeNode(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Node, Is.SameAs(node));
            Assert.That(vm.EditorLabel, Is.EqualTo("MyNode"));
        });
    }
}

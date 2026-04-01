// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class TemplateEditorViewModelTests
{
    private static readonly string ValidProductPath =
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "TestProducts", "ValidProduct"));

    private static TreeNodeModel MakeTemplateNode() =>
        new()
        {
            Text = "Main",
            Tag = "Template",
            NodePath = Path.Combine(ValidProductPath, "Templates", "Main", "Template.json")
        };

    [Test]
    public void ChangeNode_LoadsTemplateProperties()
    {
        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(MakeTemplateNode());

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.Not.Empty);
            Assert.That(vm.DatabaseIdentificationScript, Is.Not.Empty);
        });
    }

    [Test]
    public void ChangeNode_LoadsName()
    {
        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(MakeTemplateNode());

        Assert.That(vm.Name, Is.EqualTo("Main"));
    }

    [Test]
    public void ChangeNode_LoadsScriptTokens_NotNull()
    {
        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(MakeTemplateNode());

        Assert.That(vm.ScriptTokens, Is.Not.Null);
    }

    [Test]
    public void ChangeNode_WithEmptyNodePath_DoesNotThrow()
    {
        var node = new TreeNodeModel { Text = "Empty", Tag = "Template", NodePath = "" };
        var vm = new TemplateEditorViewModel();

        Assert.DoesNotThrow(() => vm.ChangeNode(node));
    }

    [Test]
    public void ChangeNode_WithInvalidPath_DoesNotThrow()
    {
        var node = new TreeNodeModel
        {
            Text = "Bad",
            Tag = "Template",
            NodePath = Path.Combine(ValidProductPath, "Templates", "NonExistent", "Template.json")
        };
        var vm = new TemplateEditorViewModel();

        Assert.DoesNotThrow(() => vm.ChangeNode(node));
    }

    [Test]
    public void EditorTitle_ReturnsTemplateName()
    {
        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(MakeTemplateNode());

        Assert.That(vm.EditorTitle, Is.EqualTo(vm.Name));
    }

    [Test]
    public void EditorTitle_IsEmptyBeforeChangeNode()
    {
        var vm = new TemplateEditorViewModel();

        Assert.That(vm.EditorTitle, Is.EqualTo(""));
    }

    [Test]
    public void ChangeNode_SetsNodeReference()
    {
        var node = MakeTemplateNode();
        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.Node, Is.SameAs(node));
    }

    [Test]
    public void ChangeNode_WithPendingTokenName_SetsSelectedTabIndex()
    {
        EditorBaseViewModel.PendingTokenName = "SomeToken";
        var node = new TreeNodeModel
        {
            Text = "Main",
            Tag = "Template",
            NodePath = Path.Combine(ValidProductPath, "Templates", "Main", "Template.json")
        };

        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.SelectedTabIndex, Is.EqualTo(1));
        Assert.That(EditorBaseViewModel.PendingTokenName, Is.Null);
    }

    [Test]
    public void ChangeNode_WithNoPendingTokenName_SetsSelectedTabIndexToZero()
    {
        // Exercises the else branch: PendingTokenName == null → SelectedTabIndex = 0
        EditorBaseViewModel.PendingTokenName = null;
        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(MakeTemplateNode());

        Assert.That(vm.SelectedTabIndex, Is.EqualTo(0));
    }

    [Test]
    public void ChangeNode_WithPendingTokenNameNotInTemplate_SelectedScriptTokenHasNullKey()
    {
        // PendingTokenName is set but the token does not exist in ScriptTokens —
        // FirstOrDefault returns default(KeyValuePair<string,string>) when no match found
        EditorBaseViewModel.PendingTokenName = "TokenThatDoesNotExist";
        var vm = new TemplateEditorViewModel();
        vm.ChangeNode(MakeTemplateNode());

        Assert.That(vm.SelectedTabIndex, Is.EqualTo(1));
        // Default KVP has null Key when no match is found
        Assert.That(vm.SelectedScriptToken?.Key, Is.Null);
    }

    [TearDown]
    public void Cleanup()
    {
        EditorBaseViewModel.PendingTokenName = null;
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class ProductEditorViewModelTests
{
    private static readonly string ValidProductPath =
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "TestProducts", "ValidProduct"));

    [TearDown]
    public void Cleanup()
    {
        EditorBaseViewModel.PendingTokenName = null;
    }

    [Test]
    public void ChangeNode_LoadsProductProperties()
    {
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.Name, Is.Not.Empty);
        Assert.That(vm.Platform, Is.Not.Empty);
    }

    [Test]
    public void ChangeNode_LoadsTemplateOrder()
    {
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.TemplateOrder, Is.Not.Empty);
    }

    [Test]
    public void ChangeNode_LoadsScriptTokens()
    {
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        // ScriptTokens may or may not be empty depending on test fixture
        Assert.That(vm.ScriptTokens, Is.Not.Null);
    }

    [Test]
    public void ChangeNode_WithPendingTokenName_SetsSelectedTabIndex()
    {
        EditorBaseViewModel.PendingTokenName = "MainDB";
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.SelectedTabIndex, Is.EqualTo(1)); // Script Tokens tab
        Assert.That(EditorBaseViewModel.PendingTokenName, Is.Null); // Cleared after use
    }

    [Test]
    public void ChangeNode_WithPendingTokenName_SelectsMatchingToken()
    {
        EditorBaseViewModel.PendingTokenName = "MainDB";
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.SelectedScriptToken, Is.Not.Null);
        Assert.That(vm.SelectedScriptToken!.Value.Key, Is.EqualTo("MainDB"));
    }

    [Test]
    public void ChangeNode_WithoutPendingTokenName_TabIndexZero()
    {
        EditorBaseViewModel.PendingTokenName = null;
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.SelectedTabIndex, Is.EqualTo(0));
    }

    [Test]
    public void EditorTitle_ReturnsName()
    {
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };
        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.EditorTitle, Is.Not.Empty);
        Assert.That(vm.EditorTitle, Is.EqualTo(vm.Name));
    }
}

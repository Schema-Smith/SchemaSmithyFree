// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: none — Community-only end-to-end scenario tests for MainWindowViewModel.

using Avalonia.Headless.NUnit;
using NSubstitute;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.FunctionalTests.Tier3_Scenarios.SqlServer;

[TestFixture]
public class NavigateAndViewScenarioTests : TestProductFixture
{
    private MainWindowViewModel _vm = null!;
    private IUserSettingsService _settingsService = null!;

    [SetUp]
    public new void SetUp()
    {
        base.SetUp();
        BuildStandardSqlServerProduct();

        _settingsService = Substitute.For<IUserSettingsService>();
        _settingsService.Settings.Returns(new UserSettings());

        _vm = new MainWindowViewModel(
            _settingsService,
            new NavigationService(),
            new EditorService(),
            new ProductTreeService(),
            new SchemaFileService());
    }

    private void LoadProduct() => _vm.LoadProductFromPath(TempDir);

    private TreeNodeModel? FindNode(string treePath)
    {
        // After LoadProduct, the tree has a Product root node wrapping the actual service roots.
        // The tree structure exposed in TreeViewModel.RootNodes is:
        //   [Product root] -> children are the service roots (Templates container, etc.)
        // We need to navigate starting from the Product root's children.
        var productRoot = _vm.TreeViewModel.RootNodes.FirstOrDefault(n => n.Tag == "Product");
        if (productRoot == null) return null;

        productRoot.EnsureExpanded();
        var serviceRoots = productRoot.Children.ToList();
        return AssertionHelpers.FindNode(serviceRoots, treePath);
    }

    private void SelectNode(TreeNodeModel node) =>
        _vm.TreeViewModel.SelectedNode = node;

    // ---------------------------------------------------------------------------
    // Test 1: Table → Script → Back
    // ---------------------------------------------------------------------------

    [AvaloniaTest]
    public void OpenProduct_NavigateTree_ViewTable_NavigateToScript_NavigateBack()
    {
        LoadProduct();

        // Navigate to Users table
        var usersNode = FindNode("Templates/Main/Tables/[dbo].[Users]");
        Assert.That(usersNode, Is.Not.Null, "Users table node not found");
        SelectNode(usersNode!);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<TableEditorViewModel>(),
            "Expected TableEditorViewModel after selecting Users table");

        // Navigate to script
        var scriptNode = FindNode("Templates/Main/Views/vw_ActiveUsers.sql");
        Assert.That(scriptNode, Is.Not.Null, "Script node not found");
        SelectNode(scriptNode!);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<SqlScriptEditorViewModel>(),
            "Expected SqlScriptEditorViewModel after selecting script");

        // Navigate back — should return to Users table
        Assert.That(_vm.CanNavigateBack, Is.True, "Should be able to navigate back");
        _vm.NavigateBackCommand.Execute(null);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<TableEditorViewModel>(),
            "Expected TableEditorViewModel after navigating back from script");
    }

    // ---------------------------------------------------------------------------
    // Test 2: Column editor with DataType verification
    // ---------------------------------------------------------------------------

    [AvaloniaTest]
    public void OpenProduct_NavigateToColumn_VerifyColumnEditor()
    {
        LoadProduct();

        var usersNode = FindNode("Templates/Main/Tables/[dbo].[Users]");
        Assert.That(usersNode, Is.Not.Null, "Users table node not found");
        usersNode!.EnsureExpanded();

        var columnsContainer = usersNode.Children
            .FirstOrDefault(c => c.Text.Equals("Columns", StringComparison.OrdinalIgnoreCase));
        Assert.That(columnsContainer, Is.Not.Null, "Columns container not found under Users");
        columnsContainer!.EnsureExpanded();

        var idNode = columnsContainer.Children
            .FirstOrDefault(c => c.Text.Equals("Id", StringComparison.OrdinalIgnoreCase));
        Assert.That(idNode, Is.Not.Null, "Id column node not found");

        SelectNode(idNode!);

        Assert.That(_vm.CurrentEditor, Is.InstanceOf<ColumnEditorViewModel>(),
            "Expected ColumnEditorViewModel after selecting Id column");
        var colEditor = (ColumnEditorViewModel)_vm.CurrentEditor;
        Assert.That(colEditor.DataType, Is.EqualTo("int"),
            "Expected DataType 'int' for Id column");
    }

    // ---------------------------------------------------------------------------
    // Test 3: Multiple-node back traversal
    // ---------------------------------------------------------------------------

    [AvaloniaTest]
    public void NavigationHistory_MultipleNodes_BackTraversesCorrectly()
    {
        LoadProduct();

        var usersNode = FindNode("Templates/Main/Tables/[dbo].[Users]");
        var ordersNode = FindNode("Templates/Main/Tables/[dbo].[Orders]");
        var scriptNode = FindNode("Templates/Main/Views/vw_ActiveUsers.sql");

        Assert.That(usersNode, Is.Not.Null, "Users node not found");
        Assert.That(ordersNode, Is.Not.Null, "Orders node not found");
        Assert.That(scriptNode, Is.Not.Null, "Script node not found");

        // Visit 3 nodes: Users → Orders → Script
        SelectNode(usersNode!);
        SelectNode(ordersNode!);
        SelectNode(scriptNode!);

        Assert.That(_vm.CurrentEditor, Is.InstanceOf<SqlScriptEditorViewModel>(),
            "Should be on SqlScriptEditorViewModel after selecting script");

        // Back → Orders
        _vm.NavigateBackCommand.Execute(null);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<TableEditorViewModel>(),
            "Expected TableEditorViewModel (Orders) after first back");
        Assert.That(_vm.TreeViewModel.SelectedNode?.Text,
            Does.Contain("Orders").IgnoreCase,
            "Selected node should be Orders after first back");

        // Back → Users
        _vm.NavigateBackCommand.Execute(null);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<TableEditorViewModel>(),
            "Expected TableEditorViewModel (Users) after second back");
        Assert.That(_vm.TreeViewModel.SelectedNode?.Text,
            Does.Contain("Users").IgnoreCase,
            "Selected node should be Users after second back");
    }

    // ---------------------------------------------------------------------------
    // Test 4: Container node shows ContainerEditorViewModel
    // ---------------------------------------------------------------------------

    [AvaloniaTest]
    public void SelectContainerNode_ShowsContainerEditor()
    {
        LoadProduct();

        var tablesNode = FindNode("Templates/Main/Tables");
        Assert.That(tablesNode, Is.Not.Null, "Tables container node not found");

        SelectNode(tablesNode!);

        Assert.That(_vm.CurrentEditor, Is.InstanceOf<ContainerEditorViewModel>(),
            "Expected ContainerEditorViewModel after selecting Tables container");
    }

    // ---------------------------------------------------------------------------
    // Test 5: Selecting the Templates root shows ContainerEditorViewModel
    // ---------------------------------------------------------------------------

    [AvaloniaTest]
    public void SelectProductRoot_ShowsContainerEditor()
    {
        LoadProduct();

        // The tree root is a Product node. Its first child is the Templates container.
        var productRoot = _vm.TreeViewModel.RootNodes.FirstOrDefault(n => n.Tag == "Product");
        Assert.That(productRoot, Is.Not.Null, "Product root node not found");

        productRoot!.EnsureExpanded();
        var templatesNode = productRoot.Children.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesNode, Is.Not.Null, "Templates container not found under Product root");

        SelectNode(templatesNode!);

        Assert.That(_vm.CurrentEditor, Is.InstanceOf<ContainerEditorViewModel>(),
            "Expected ContainerEditorViewModel after selecting Templates container");
    }

    // ---------------------------------------------------------------------------
    // Test 6: Deep navigation — Template → Table → Column — editor changes each step
    // ---------------------------------------------------------------------------

    [AvaloniaTest]
    public void NavigateDeep_EditorSwitchesCorrectly()
    {
        LoadProduct();

        // Step 1: Select Template node
        var templateNode = FindNode("Templates/Main");
        Assert.That(templateNode, Is.Not.Null, "Main template node not found");
        SelectNode(templateNode!);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<TemplateEditorViewModel>(),
            "Expected TemplateEditorViewModel after selecting Main template");

        // Step 2: Select Table node
        var tableNode = FindNode("Templates/Main/Tables/[dbo].[Users]");
        Assert.That(tableNode, Is.Not.Null, "Users table node not found");
        SelectNode(tableNode!);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<TableEditorViewModel>(),
            "Expected TableEditorViewModel after selecting Users table");

        // Step 3: Select Column node
        tableNode!.EnsureExpanded();
        var columnsContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Columns", StringComparison.OrdinalIgnoreCase));
        Assert.That(columnsContainer, Is.Not.Null, "Columns container not found under Users");
        columnsContainer!.EnsureExpanded();

        var nameNode = columnsContainer.Children
            .FirstOrDefault(c => c.Text.Equals("Name", StringComparison.OrdinalIgnoreCase));
        Assert.That(nameNode, Is.Not.Null, "Name column node not found");

        SelectNode(nameNode!);
        Assert.That(_vm.CurrentEditor, Is.InstanceOf<ColumnEditorViewModel>(),
            "Expected ColumnEditorViewModel after selecting Name column");
    }
}

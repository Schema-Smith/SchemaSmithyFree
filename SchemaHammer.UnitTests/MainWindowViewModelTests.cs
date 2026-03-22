using NSubstitute;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        IUserSettingsService? settings = null,
        INavigationService? nav = null,
        IEditorService? editor = null,
        IProductTreeService? tree = null)
    {
        settings ??= Substitute.For<IUserSettingsService>();
        settings.Settings.Returns(new UserSettings());
        nav ??= new NavigationService();
        editor ??= new EditorService();
        tree ??= Substitute.For<IProductTreeService>();
        return new MainWindowViewModel(settings, nav, editor, tree);
    }

    [Test]
    public void Constructor_SetsDefaultTitle()
    {
        var vm = CreateViewModel();
        Assert.That(vm.Title, Is.EqualTo("SchemaHammer Community"));
    }

    [Test]
    public void Constructor_SetsWelcomeEditor()
    {
        var vm = CreateViewModel();
        Assert.That(vm.CurrentEditor, Is.TypeOf<WelcomeViewModel>());
    }

    [Test]
    public void ProductStatus_DefaultsToNoProduct()
    {
        var vm = CreateViewModel();
        Assert.That(vm.ProductStatus, Is.EqualTo("NO PRODUCT"));
    }

    [Test]
    public void SaveWindowState_PersistsToSettings()
    {
        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());
        var vm = CreateViewModel(settings: settingsService);

        vm.SaveWindowState(true, 100, 200, 1024, 768);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Settings.IsMaximized, Is.True);
            Assert.That(vm.Settings.WindowX, Is.EqualTo(100));
            Assert.That(vm.Settings.WindowY, Is.EqualTo(200));
            Assert.That(vm.Settings.WindowWidth, Is.EqualTo(1024));
            Assert.That(vm.Settings.WindowHeight, Is.EqualTo(768));
        });
        settingsService.Received(1).Save();
    }

    [Test]
    public void NavigateBack_PopsHistoryAndSetsSelectedNode()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        var node1 = new TreeNodeModel { Text = "Node1", Tag = "Folder" };
        var node2 = new TreeNodeModel { Text = "Node2", Tag = "Folder" };
        nav.Push(node1);
        nav.Push(node2);

        vm.NavigateBackCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.TreeViewModel.SelectedNode, Is.EqualTo(node2));
            Assert.That(vm.CanNavigateBack, Is.True);
            Assert.That(vm.NavigationHistory, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void NavigateBack_WithSuppression_DoesNotRePushToHistory()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        // Simulate selecting node1, then node2 (which pushes node1 to history)
        var node1 = new TreeNodeModel { Text = "Node1", Tag = "Table" };
        var node2 = new TreeNodeModel { Text = "Node2", Tag = "Table" };

        // Manually push node1 to history as if user selected node1 then node2
        nav.Push(node1);

        // Now navigate back — should set SelectedNode to node1 without re-pushing
        vm.NavigateBackCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.TreeViewModel.SelectedNode, Is.EqualTo(node1));
            Assert.That(nav.CanGoBack, Is.False);
            Assert.That(vm.CanNavigateBack, Is.False);
        });
    }

    [Test]
    public void OnNodeSelected_ChangesCurrentEditor()
    {
        var editorService = Substitute.For<IEditorService>();
        var placeholder = new TableEditorViewModel();
        var node = new TreeNodeModel { Text = "MyTable", Tag = "Table" };
        editorService.GetEditor(node).Returns(placeholder);

        var vm = CreateViewModel(editor: editorService);

        // Trigger OnNodeSelected via TreeViewModel.SelectedNode
        vm.TreeViewModel.SelectedNode = node;

        Assert.That(vm.CurrentEditor, Is.EqualTo(placeholder));
    }

    [Test]
    public void OnNodeSelected_PushesPreviousNodeToHistory()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        var node1 = new TreeNodeModel { Text = "Node1", Tag = "Table" };
        var node2 = new TreeNodeModel { Text = "Node2", Tag = "Table" };

        vm.TreeViewModel.SelectedNode = node1;
        vm.TreeViewModel.SelectedNode = node2;

        Assert.Multiple(() =>
        {
            Assert.That(nav.History, Has.Count.EqualTo(1));
            Assert.That(nav.History[0], Is.EqualTo(node1));
            Assert.That(vm.CanNavigateBack, Is.True);
        });
    }

    [Test]
    public void ReloadTree_CallsProductTreeServiceReload()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var roots = new List<TreeNodeModel> { new() { Text = "Root", Tag = "Product" } };
        treeService.ReloadProduct().Returns(roots);

        var vm = CreateViewModel(tree: treeService);
        vm.ReloadTreeCommand.Execute(null);

        treeService.Received(1).ReloadProduct();
        Assert.That(vm.TreeViewModel.RootNodes, Has.Count.EqualTo(1));
    }

    [Test]
    public void Title_UpdatesWhenProductLoaded()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var roots = new List<TreeNodeModel> { new() { Text = "Root", Tag = "Product" } };
        treeService.LoadProduct(Arg.Any<string>()).Returns(roots);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "TestProduct" });

        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());

        var vm = CreateViewModel(settings: settingsService, tree: treeService);
        vm.LoadProductFromPath("/some/path");

        Assert.Multiple(() =>
        {
            Assert.That(vm.Title, Does.Contain("TestProduct"));
            Assert.That(vm.ProductStatus, Is.EqualTo("TestProduct"));
        });
        settingsService.Received().AddRecentProduct("/some/path");
    }

    [Test]
    public void NavigateToHistory_NavigatesToSpecificEntry()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        var node1 = new TreeNodeModel { Text = "Node1", Tag = "Folder" };
        var node2 = new TreeNodeModel { Text = "Node2", Tag = "Folder" };
        var node3 = new TreeNodeModel { Text = "Node3", Tag = "Folder" };
        nav.Push(node1);
        nav.Push(node2);
        nav.Push(node3);

        // Navigate to index 1 (node2) — removes node2 and node3 from history
        vm.NavigateToHistoryCommand.Execute(1);

        Assert.Multiple(() =>
        {
            Assert.That(vm.TreeViewModel.SelectedNode, Is.EqualTo(node2));
            Assert.That(nav.History, Has.Count.EqualTo(1));
            Assert.That(nav.History[0], Is.EqualTo(node1));
        });
    }

    [Test]
    public void NavigationHistory_IsReversedOrder()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        var node1 = new TreeNodeModel { Text = "First", Tag = "Folder" };
        var node2 = new TreeNodeModel { Text = "Second", Tag = "Folder" };
        var node3 = new TreeNodeModel { Text = "Third", Tag = "Folder" };

        // Select nodes to build history via OnNodeSelected
        vm.TreeViewModel.SelectedNode = node1;
        vm.TreeViewModel.SelectedNode = node2;
        vm.TreeViewModel.SelectedNode = node3;

        // History should be [node1, node2], NavigationHistory reversed = [node2, node1]
        Assert.Multiple(() =>
        {
            Assert.That(vm.NavigationHistory, Has.Count.EqualTo(2));
            Assert.That(vm.NavigationHistory[0].Text, Is.EqualTo("Second"));
            Assert.That(vm.NavigationHistory[1].Text, Is.EqualTo("First"));
        });
    }

    [Test]
    public void Constructor_CreatesTreeViewModel()
    {
        var vm = CreateViewModel();
        Assert.That(vm.TreeViewModel, Is.Not.Null);
    }

    [Test]
    public void LoadRecentProduct_WithEmptyPath_DoesNothing()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var vm = CreateViewModel(tree: treeService);

        vm.LoadRecentProductCommand.Execute("");

        treeService.DidNotReceive().LoadProduct(Arg.Any<string>());
    }
}

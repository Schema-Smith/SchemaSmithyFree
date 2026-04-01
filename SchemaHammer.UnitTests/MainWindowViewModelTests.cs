// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NSubstitute;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.Themes;
using SchemaHammer.ViewModels;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        IUserSettingsService? settings = null,
        INavigationService? nav = null,
        IEditorService? editor = null,
        IProductTreeService? tree = null,
        ISchemaFileService? schemaFile = null)
    {
        settings ??= Substitute.For<IUserSettingsService>();
        settings.Settings.Returns(new UserSettings());
        nav ??= new NavigationService();
        editor ??= new EditorService();
        tree ??= Substitute.For<IProductTreeService>();
        schemaFile ??= Substitute.For<ISchemaFileService>();
        return new MainWindowViewModel(settings, nav, editor, tree, schemaFile);
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

    [Test]
    public void LoadRecentProduct_WithNullPath_DoesNothing()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var vm = CreateViewModel(tree: treeService);

        vm.LoadRecentProductCommand.Execute(null);

        treeService.DidNotReceive().LoadProduct(Arg.Any<string>());
    }

    [Test]
    public void LoadRecentProduct_WithNonExistentPath_DoesNothing()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var vm = CreateViewModel(tree: treeService);

        var nonExistent = Path.Combine(Path.GetTempPath(), "nonexistent_product_" + Guid.NewGuid().ToString("N"));
        vm.LoadRecentProductCommand.Execute(nonExistent);

        treeService.DidNotReceive().LoadProduct(Arg.Any<string>());
    }

    [Test]
    public void Initialize_WithNoRecentProducts_DoesNothing()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings { RecentProducts = [] });

        var vm = CreateViewModel(settings: settingsService, tree: treeService);
        vm.Initialize();

        treeService.DidNotReceive().LoadProduct(Arg.Any<string>());
        Assert.That(vm.TreeViewModel.RootNodes, Is.Empty);
    }

    [Test]
    public void Initialize_WithNonExistentProduct_DoesNothing()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var settingsService = Substitute.For<IUserSettingsService>();
        var stalePath = Path.Combine(Path.GetTempPath(), "nonexistent_product_" + Guid.NewGuid().ToString("N"));
        settingsService.Settings.Returns(new UserSettings { RecentProducts = [stalePath] });

        var vm = CreateViewModel(settings: settingsService, tree: treeService);
        vm.Initialize();

        treeService.DidNotReceive().LoadProduct(Arg.Any<string>());
        Assert.That(vm.TreeViewModel.RootNodes, Is.Empty);
    }

    [Test]
    public void Initialize_WithRecentProduct_LoadsProduct()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var settingsService = Substitute.For<IUserSettingsService>();

        // Use a temp directory with a Product.json to simulate a real product
        var tempProductDir = Path.Combine(
            Path.GetTempPath(),
            "TestProduct_" + Guid.NewGuid().ToString("N"));
        tempProductDir = tempProductDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Directory.CreateDirectory(tempProductDir);
        var productJsonPath = Path.Combine(tempProductDir, "Product.json");
        File.WriteAllText(productJsonPath, "{\"Name\":\"TestProduct\",\"Templates\":[]}");

        try
        {
            var settings = new UserSettings();
            settings.RecentProducts.Add(tempProductDir);
            // Set up Settings AFTER creating the substitute and construct the VM directly
            // to avoid CreateViewModel overwriting the settings mock
            settingsService.Settings.Returns(settings);
            treeService.LoadProduct(Arg.Any<string>()).Returns([]);
            treeService.Product.Returns(new Schema.Domain.Product { Name = "TestProduct" });

            var vm = new MainWindowViewModel(settingsService, new NavigationService(), new EditorService(), treeService, Substitute.For<ISchemaFileService>());
            vm.Initialize();

            treeService.Received(1).LoadProduct(tempProductDir);
            Assert.That(vm.TreeViewModel.RootNodes, Has.Count.EqualTo(1));
        }
        finally
        {
            Directory.Delete(tempProductDir, true);
        }
    }

    [Test]
    public void NavigateBack_WithEmptyHistory_DoesNothing()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        // No nodes pushed, NavigateBack should do nothing
        vm.NavigateBackCommand.Execute(null);

        Assert.That(vm.TreeViewModel.SelectedNode, Is.Null);
        Assert.That(vm.CanNavigateBack, Is.False);
    }

    [Test]
    public void NavigateToHistory_WithInvalidIndex_DoesNothing()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        // No history, index 5 is out of range
        vm.NavigateToHistoryCommand.Execute(5);

        Assert.That(vm.TreeViewModel.SelectedNode, Is.Null);
    }

    [Test]
    public void SelectNodeFromSearch_SetsSelectedNode()
    {
        var vm = CreateViewModel();
        var node = new TreeNodeModel { Text = "Found", Tag = "Table" };

        vm.SelectNodeFromSearch(node);

        Assert.That(vm.TreeViewModel.SelectedNode, Is.EqualTo(node));
    }

    [Test]
    public void SelectNodeFromSearch_AddsToHistory()
    {
        var nav = new NavigationService();
        var vm = CreateViewModel(nav: nav);

        var node1 = new TreeNodeModel { Text = "First", Tag = "Table" };
        var node2 = new TreeNodeModel { Text = "FromSearch", Tag = "Table" };

        vm.TreeViewModel.SelectedNode = node1;
        vm.SelectNodeFromSearch(node2);

        Assert.That(nav.History, Has.Count.EqualTo(1));
        Assert.That(nav.History[0], Is.EqualTo(node1));
    }

    [Test]
    public void ProductStatusTooltip_SetDuringLoadProduct()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var roots = new List<TreeNodeModel> { new() { Text = "Root", Tag = "Product" } };
        treeService.LoadProduct(Arg.Any<string>()).Returns(roots);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "TestProduct" });
        treeService.SearchList.Returns(new List<TreeNodeModel> { new(), new(), new() });

        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());

        var vm = CreateViewModel(settings: settingsService, tree: treeService);
        vm.LoadProductFromPath("/some/path");

        Assert.That(vm.ProductStatusTooltip, Does.Contain("/some/path"));
        Assert.That(vm.ProductStatusTooltip, Does.Contain("nodes"));
    }

    [Test]
    public void UpdateSchemaFiles_NoProduct_DoesNotThrow()
    {
        var vm = CreateViewModel();
        Assert.DoesNotThrow(() => vm.UpdateSchemaFilesCommand.Execute(null));
    }

    [Test]
    public void SearchTreeCommand_FiresOpenSearchRequested()
    {
        var vm = CreateViewModel();
        string? receivedTab = null;
        vm.OpenSearchRequested += tab => receivedTab = tab;

        vm.SearchTreeCommand.Execute(null);

        Assert.That(receivedTab, Is.EqualTo("Tree"));
    }

    [Test]
    public void SearchCodeCommand_FiresOpenSearchRequested()
    {
        var vm = CreateViewModel();
        string? receivedTab = null;
        vm.OpenSearchRequested += tab => receivedTab = tab;

        vm.SearchCodeCommand.Execute(null);

        Assert.That(receivedTab, Is.EqualTo("Code"));
    }

    [Test]
    public void ShowAboutCommand_FiresShowAboutRequested()
    {
        var vm = CreateViewModel();
        var fired = false;
        vm.ShowAboutRequested += () => fired = true;

        vm.ShowAboutCommand.Execute(null);

        Assert.That(fired, Is.True);
    }

    [Test]
    public void SearchTreeCommand_NoSubscriber_DoesNotThrow()
    {
        var vm = CreateViewModel();
        Assert.DoesNotThrow(() => vm.SearchTreeCommand.Execute(null));
    }

    [Test]
    public void SearchCodeCommand_NoSubscriber_DoesNotThrow()
    {
        var vm = CreateViewModel();
        Assert.DoesNotThrow(() => vm.SearchCodeCommand.Execute(null));
    }

    [Test]
    public void ShowAboutCommand_NoSubscriber_DoesNotThrow()
    {
        var vm = CreateViewModel();
        Assert.DoesNotThrow(() => vm.ShowAboutCommand.Execute(null));
    }

    [Test]
    public void ExitCommand_WithNoApplication_DoesNotThrow()
    {
        var vm = CreateViewModel();
        Assert.DoesNotThrow(() => vm.ExitCommand.Execute(null));
    }

    [Test]
    public void OpenThemeSettingsCommand_WithNoThemeService_DoesNotThrow()
    {
        var vm = CreateViewModel();
        // ThemeService.Instance is null in test context — should return early
        Assert.DoesNotThrow(() => vm.OpenThemeSettingsCommand.Execute(null));
    }

    [Test]
    public void ProductStatusTooltip_DefaultsToEmpty()
    {
        var vm = CreateViewModel();
        Assert.That(vm.ProductStatusTooltip, Is.EqualTo(""));
    }

    [Test]
    public void ProductStatusTooltip_ContainsNodeCount()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var roots = new List<TreeNodeModel> { new() { Text = "Root", Tag = "Product" } };
        treeService.LoadProduct(Arg.Any<string>()).Returns(roots);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "Test" });
        treeService.SearchList.Returns(new List<TreeNodeModel> { new(), new(), new(), new(), new() });

        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());

        var vm = CreateViewModel(settings: settingsService, tree: treeService);
        vm.LoadProductFromPath("/test/path");

        Assert.That(vm.ProductStatusTooltip, Does.Contain("5"));
        Assert.That(vm.ProductStatusTooltip, Does.Contain("ms"));
    }

    [Test]
    public void LoadProductFromPath_WithNullProductName_UsesFolderName()
    {
        var treeService = Substitute.For<IProductTreeService>();
        treeService.LoadProduct(Arg.Any<string>()).Returns([]);
        treeService.Product.Returns((Schema.Domain.Product?)null);

        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());

        var vm = CreateViewModel(settings: settingsService, tree: treeService);
        vm.LoadProductFromPath("/some/MyFolder");

        Assert.That(vm.Title, Does.Contain("Unknown"));
        Assert.That(vm.ProductStatus, Is.EqualTo("NO PRODUCT"));
    }

    [Test]
    public void LoadProductFromPath_WithLastSelectedNodePath_RestoresSelection()
    {
        var treeService = Substitute.For<IProductTreeService>();
        var childNode = new TreeNodeModel { Text = "Main", Tag = "Template" };
        treeService.LoadProduct(Arg.Any<string>()).Returns(new List<TreeNodeModel> { childNode });
        treeService.Product.Returns(new Schema.Domain.Product { Name = "TestProduct" });

        var settingsService = Substitute.For<IUserSettingsService>();
        var settings = new UserSettings { LastSelectedNodePath = @"TestProduct\Main" };
        settingsService.Settings.Returns(settings);

        var vm = new MainWindowViewModel(settingsService, new NavigationService(), new EditorService(), treeService, Substitute.For<ISchemaFileService>());
        vm.LoadProductFromPath("/some/path");

        Assert.That(vm.TreeViewModel.SelectedNode, Is.SameAs(childNode));
    }

    [Test]
    public void LoadProductFromPath_WithNonMatchingLastSelectedNodePath_DoesNotSetSelection()
    {
        var treeService = Substitute.For<IProductTreeService>();
        treeService.LoadProduct(Arg.Any<string>()).Returns([]);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "TestProduct" });

        var settingsService = Substitute.For<IUserSettingsService>();
        var settings = new UserSettings { LastSelectedNodePath = @"TestProduct\NonExistent\Path" };
        settingsService.Settings.Returns(settings);

        var vm = new MainWindowViewModel(settingsService, new NavigationService(), new EditorService(), treeService, Substitute.For<ISchemaFileService>());
        vm.LoadProductFromPath("/some/path");

        Assert.That(vm.TreeViewModel.SelectedNode, Is.Null);
    }

    [Test]
    public void LoadProductFromPath_WithEmptyLastSelectedNodePath_DoesNotSetSelection()
    {
        var treeService = Substitute.For<IProductTreeService>();
        treeService.LoadProduct(Arg.Any<string>()).Returns([]);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "TestProduct" });

        var settingsService = Substitute.For<IUserSettingsService>();
        var settings = new UserSettings { LastSelectedNodePath = "" };
        settingsService.Settings.Returns(settings);

        var vm = new MainWindowViewModel(settingsService, new NavigationService(), new EditorService(), treeService, Substitute.For<ISchemaFileService>());
        vm.LoadProductFromPath("/some/path");

        Assert.That(vm.TreeViewModel.SelectedNode, Is.Null);
    }

    [Test]
    public void LoadProductFromPath_ResetsCurrentEditorToWelcome()
    {
        var treeService = Substitute.For<IProductTreeService>();
        treeService.LoadProduct(Arg.Any<string>()).Returns([]);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "Test" });

        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());

        var vm = CreateViewModel(settings: settingsService, tree: treeService);
        vm.LoadProductFromPath("/some/path");

        Assert.That(vm.CurrentEditor, Is.TypeOf<WelcomeViewModel>());
    }

    [Test]
    public void LoadProductFromPath_ClearsNavigationHistory()
    {
        var nav = new NavigationService();
        nav.Push(new TreeNodeModel { Text = "Old", Tag = "Table" });

        var treeService = Substitute.For<IProductTreeService>();
        treeService.LoadProduct(Arg.Any<string>()).Returns([]);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "Test" });

        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());

        var vm = new MainWindowViewModel(settingsService, nav, new EditorService(), treeService, Substitute.For<ISchemaFileService>());
        vm.LoadProductFromPath("/some/path");

        Assert.That(vm.CanNavigateBack, Is.False);
        Assert.That(vm.NavigationHistory, Is.Empty);
    }

    [Test]
    public void ReloadTree_ResetsCurrentEditorToWelcome()
    {
        var treeService = Substitute.For<IProductTreeService>();
        treeService.ReloadProduct().Returns([]);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "Test", FilePath = "/some/Product.json" });

        var vm = CreateViewModel(tree: treeService);
        vm.ReloadTreeCommand.Execute(null);

        Assert.That(vm.CurrentEditor, Is.TypeOf<WelcomeViewModel>());
    }

    [Test]
    public void ReloadTree_SetsProductStatusTooltip()
    {
        var treeService = Substitute.For<IProductTreeService>();
        treeService.ReloadProduct().Returns([]);
        treeService.Product.Returns(new Schema.Domain.Product { Name = "Test", FilePath = "/some/path/Product.json" });
        treeService.SearchList.Returns(new List<TreeNodeModel> { new(), new() });

        var vm = CreateViewModel(tree: treeService);
        vm.ReloadTreeCommand.Execute(null);

        Assert.That(vm.ProductStatusTooltip, Does.Contain("ms"));
    }

    [Test]
    public void OpenThemeSettings_WithThemeServiceInstance_TogglesTheme()
    {
        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());
        var vm = CreateViewModel(settings: settingsService);

        var themeService = new ThemeService();
        ThemeService.Instance = themeService;
        try
        {
            vm.OpenThemeSettingsCommand.Execute(null);

            Assert.That(settingsService.Settings.ActiveThemeName, Is.Not.Null);
            settingsService.Received().Save();
        }
        finally
        {
            ThemeService.Instance = null;
        }
    }

    [Test]
    public void ChooseProduct_WithNoApplication_DoesNotThrow()
    {
        var vm = CreateViewModel();
        Assert.DoesNotThrowAsync(async () => await vm.ChooseProductCommand.ExecuteAsync(null));
    }

    [Test]
    public void UpdateSchemaFiles_ProductWithNullFilePath_DoesNotThrow()
    {
        // Product is not null but FilePath is null → productPath is empty → early return
        var treeService = Substitute.For<IProductTreeService>();
        treeService.Product.Returns(new Schema.Domain.Product { Name = "Test", FilePath = null });

        var vm = CreateViewModel(tree: treeService);
        Assert.DoesNotThrowAsync(async () => await vm.UpdateSchemaFilesCommand.ExecuteAsync(null));
    }

    [Test]
    public void UpdateSchemaFiles_ProductWithEmptyFilePath_DoesNotThrow()
    {
        // Product is not null but FilePath is "" → productPath is "" → early return
        var treeService = Substitute.For<IProductTreeService>();
        treeService.Product.Returns(new Schema.Domain.Product { Name = "Test", FilePath = "" });

        var vm = CreateViewModel(tree: treeService);
        Assert.DoesNotThrowAsync(async () => await vm.UpdateSchemaFilesCommand.ExecuteAsync(null));
    }

    [Test]
    public void OnNodeSelected_SetsLastSelectedNodePath()
    {
        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());

        var vm = CreateViewModel(settings: settingsService);
        var node = new TreeNodeModel { Text = "TestNode", Tag = "Table" };

        vm.TreeViewModel.SelectedNode = node;

        Assert.That(settingsService.Settings.LastSelectedNodePath, Is.EqualTo("TestNode"));
    }

    [Test]
    public void OnNodeSelected_EditorGetsNavigateToNodeCallback()
    {
        var editorService = Substitute.For<IEditorService>();
        var editor = new TableEditorViewModel();
        var node = new TreeNodeModel { Text = "MyTable", Tag = "Table" };
        editorService.GetEditor(node).Returns(editor);

        var vm = CreateViewModel(editor: editorService);
        vm.TreeViewModel.SelectedNode = node;

        Assert.That(editor.NavigateToNode, Is.Not.Null);
    }
}

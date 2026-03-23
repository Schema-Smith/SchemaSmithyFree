using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;
using NSubstitute;

namespace SchemaHammer.UnitTests;

public class SearchViewModelTests
{
    private static IProductTreeService CreateTreeService(params TreeNodeModel[] searchListItems)
    {
        var service = Substitute.For<IProductTreeService>();
        var searchList = new List<TreeNodeModel>(searchListItems);
        service.SearchList.Returns(searchList);
        service.Templates.Returns(new Dictionary<string, Schema.Domain.Template>(StringComparer.OrdinalIgnoreCase));
        service.Product.Returns((Schema.Domain.Product?)null);
        return service;
    }

    [Test]
    public void SearchTree_Contains_FindsMatchingNodes()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "Test";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));
        Assert.That(vm.TreeSearchResults[0].Name, Is.EqualTo("TestTable"));
        Assert.That(vm.TreeSearchResults[0].Template, Is.EqualTo("Main"));
        Assert.That(vm.TreeSearchResults[0].Type, Is.EqualTo("Table"));
    }

    [Test]
    public void SearchTree_BeginsWith_MatchesCorrectly()
    {
        var node1 = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var node2 = new TreeNodeModel { Text = "MyTest", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node1, node2);
        var vm = new SearchViewModel(treeService);

        vm.SelectedSearchType = "Begins With";
        vm.TreeSearchTerm = "Test";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));
        Assert.That(vm.TreeSearchResults[0].Name, Is.EqualTo("TestTable"));
    }

    [Test]
    public void SearchTree_EndsWith_MatchesCorrectly()
    {
        var node1 = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var node2 = new TreeNodeModel { Text = "TableTest", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node1, node2);
        var vm = new SearchViewModel(treeService);

        vm.SelectedSearchType = "Ends With";
        vm.TreeSearchTerm = "Test";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));
        Assert.That(vm.TreeSearchResults[0].Name, Is.EqualTo("TableTest"));
    }

    [Test]
    public void SearchTree_CaseInsensitive()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "testtable";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));
    }

    [Test]
    public void SearchTree_EmptyTerm_ClearsResults()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "Test";
        vm.SearchTreeCommand.Execute(null);
        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));

        vm.TreeSearchTerm = "";
        vm.SearchTreeCommand.Execute(null);
        Assert.That(vm.TreeSearchResults, Is.Empty);
    }

    [Test]
    public void SearchTree_FiltersOutContainerNodes()
    {
        var table = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var container = new TreeNodeModel { Text = "Tables", Tag = "Tables" };
        var folder = new TreeNodeModel { Text = "Templates", Tag = "Templates" };
        var scriptFolder = new TreeNodeModel { Text = "Before", Tag = "Sql Script FolderContainer" };
        var treeService = CreateTreeService(table, container, folder, scriptFolder);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "T";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));
        Assert.That(vm.TreeSearchResults[0].Name, Is.EqualTo("TestTable"));
    }

    [Test]
    public void SearchTree_NoMatch_ReturnsEmpty()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "xyz";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Is.Empty);
    }

    [Test]
    public void SelectTreeResult_SetsSelectedResultNode()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table" };
        var result = new SearchResultItem { Name = "TestTable", Node = node };

        var vm = new SearchViewModel(CreateTreeService());
        vm.SelectTreeResultCommand.Execute(result);

        Assert.That(vm.SelectedResultNode, Is.SameAs(node));
    }

    [Test]
    public void SelectTreeResult_NullItem_DoesNothing()
    {
        var vm = new SearchViewModel(CreateTreeService());
        vm.SelectTreeResultCommand.Execute(null);
        Assert.That(vm.SelectedResultNode, Is.Null);
    }

    [Test]
    public void Constructor_DefaultsToContainsSearch()
    {
        var vm = new SearchViewModel(CreateTreeService());
        Assert.That(vm.SelectedSearchType, Is.EqualTo("Contains"));
    }

    [Test]
    public void Constructor_DefaultTabIndex_Zero()
    {
        var vm = new SearchViewModel(CreateTreeService());
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_WithCodeTab_SetsTabIndex()
    {
        var vm = new SearchViewModel(CreateTreeService(), "Code");
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(1));
    }

    // --- Code Search Tests (use real ProductTreeService + ValidProduct) ---

    private static readonly string ValidProductPath =
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "TestProducts", "ValidProduct"));

    private static SearchViewModel CreateRealSearchViewModel()
    {
        var treeService = new ProductTreeService();
        treeService.LoadProduct(ValidProductPath);
        return new SearchViewModel(treeService);
    }

    [Test]
    public void SearchCode_FindsTextInSqlScripts()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "CREATE";
        vm.SearchCodeCommand.Execute(null);

        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        Assert.That(scriptResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_FindsTableByName()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "TestTable";
        vm.SearchCodeCommand.Execute(null);

        var tableResults = vm.CodeSearchResults.Where(r => r.Type == "Table").ToList();
        Assert.That(tableResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_FindsColumnByName()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "ParentID";
        vm.SearchCodeCommand.Execute(null);

        var columnResults = vm.CodeSearchResults.Where(r => r.Type == "Column").ToList();
        Assert.That(columnResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_FindsScriptTokenByKey()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "MainDB";
        vm.SearchCodeCommand.Execute(null);

        var tokenResults = vm.CodeSearchResults.Where(r => r.Type == "Script Token").ToList();
        Assert.That(tokenResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_FindsScriptTokenByValue()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "TestMain";
        vm.SearchCodeCommand.Execute(null);

        var tokenResults = vm.CodeSearchResults.Where(r => r.Type == "Script Token").ToList();
        Assert.That(tokenResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_EmptyTerm_ClearsResults()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "CREATE";
        vm.SearchCodeCommand.Execute(null);
        Assert.That(vm.CodeSearchResults, Is.Not.Empty);

        vm.CodeSearchTerm = "";
        vm.SearchCodeCommand.Execute(null);
        Assert.That(vm.CodeSearchResults, Is.Empty);
    }

    [Test]
    public void SearchCode_NoMatch_ReturnsEmpty()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "ZZZZNOTFOUND999";
        vm.SearchCodeCommand.Execute(null);
        Assert.That(vm.CodeSearchResults, Is.Empty);
    }

    [Test]
    public void SearchCode_CaseInsensitive()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "testtable";
        vm.SearchCodeCommand.Execute(null);
        Assert.That(vm.CodeSearchResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_ProductLevelScripts_ShowProductTemplate()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "CREATE";
        vm.SearchCodeCommand.Execute(null);
        Assert.That(vm.CodeSearchResults.All(r => r.Template != null), Is.True);
    }

    [Test]
    public void SelectCodeResult_TokenResult_SetsPendingTokenName()
    {
        var node = new TreeNodeModel { Text = "Main", Tag = "Template" };
        var result = new SearchResultItem
        {
            Name = "{{{MainDB}}}",
            Type = "Script Token",
            Node = node
        };

        var vm = new SearchViewModel(CreateTreeService());
        vm.SelectCodeResultCommand.Execute(result);

        Assert.That(SchemaHammer.ViewModels.Editors.EditorBaseViewModel.PendingTokenName, Is.EqualTo("MainDB"));
        Assert.That(vm.SelectedResultNode, Is.SameAs(node));
        SchemaHammer.ViewModels.Editors.EditorBaseViewModel.PendingTokenName = null;
    }

    [Test]
    public void SelectCodeResult_NonTokenResult_DoesNotSetPendingTokenName()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table" };
        var result = new SearchResultItem
        {
            Name = "TestTable",
            Type = "Table",
            Node = node
        };

        var vm = new SearchViewModel(CreateTreeService());
        SchemaHammer.ViewModels.Editors.EditorBaseViewModel.PendingTokenName = null;
        vm.SelectCodeResultCommand.Execute(result);

        Assert.That(SchemaHammer.ViewModels.Editors.EditorBaseViewModel.PendingTokenName, Is.Null);
        Assert.That(vm.SelectedResultNode, Is.SameAs(node));
    }

    [Test]
    public void SelectCodeResult_NullItem_DoesNothing()
    {
        var vm = new SearchViewModel(CreateTreeService());
        vm.SelectCodeResultCommand.Execute(null);
        Assert.That(vm.SelectedResultNode, Is.Null);
    }

    [Test]
    public void SelectCodeResult_NullNode_DoesNothing()
    {
        var result = new SearchResultItem { Name = "Test", Type = "Table", Node = null };
        var vm = new SearchViewModel(CreateTreeService());
        vm.SelectCodeResultCommand.Execute(result);
        Assert.That(vm.SelectedResultNode, Is.Null);
    }

    [Test]
    public void SearchCode_FindsIndexByName()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "PK_";
        vm.SearchCodeCommand.Execute(null);

        var indexResults = vm.CodeSearchResults.Where(r => r.Type == "Index").ToList();
        Assert.That(indexResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_FindsForeignKeyByName()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "FK_";
        vm.SearchCodeCommand.Execute(null);

        var fkResults = vm.CodeSearchResults.Where(r => r.Type == "Foreign Key").ToList();
        Assert.That(fkResults, Is.Not.Empty);
    }
}

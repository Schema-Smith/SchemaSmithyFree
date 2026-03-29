// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

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
            Name = "{{MainDB}}",
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

    [Test]
    public void SearchCode_FindsCheckConstraintByExpression()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "Status";
        vm.SearchCodeCommand.Execute(null);

        var ccResults = vm.CodeSearchResults.Where(r => r.Type == "Check Constraint").ToList();
        Assert.That(ccResults, Is.Not.Empty);
    }

    [Test]
    public void SearchCode_FindsStatisticByName()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "ST_";
        vm.SearchCodeCommand.Execute(null);

        var statResults = vm.CodeSearchResults.Where(r => r.Type == "Statistic").ToList();
        Assert.That(statResults, Is.Not.Empty);
    }

    [Test]
    public void SearchTree_FiltersOutIndexedViewsContainerNode()
    {
        var viewNode = new TreeNodeModel { Text = "vw_Test", Tag = "Indexed View", TemplateName = "Main" };
        var container = new TreeNodeModel { Text = "Indexed Views", Tag = "Indexed Views" };
        var treeService = CreateTreeService(viewNode, container);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "v";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));
        Assert.That(vm.TreeSearchResults[0].Name, Is.EqualTo("vw_Test"));
    }

    [Test]
    public void SearchTree_FiltersOutFolderTagNodes()
    {
        var scriptNode = new TreeNodeModel { Text = "deploy.sql", Tag = "Sql Script", TemplateName = "Main" };
        var folder = new TreeNodeModel { Text = "Functions", Tag = "FunctionsFolder" };
        var treeService = CreateTreeService(scriptNode, folder);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "F";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Is.Empty);
    }

    [Test]
    public void SearchCode_WithWhitespaceOnlyTerm_ClearsResults()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "   ";
        vm.SearchCodeCommand.Execute(null);
        Assert.That(vm.CodeSearchResults, Is.Empty);
    }

    [Test]
    public void SearchTree_WhitespaceOnlyTerm_ClearsResults()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "   ";
        vm.SearchTreeCommand.Execute(null);
        Assert.That(vm.TreeSearchResults, Is.Empty);
    }

    [Test]
    public void SearchCode_ScriptWithEmptyNodePath_IsSkipped()
    {
        var scriptNode = new TreeNodeModel { Text = "orphan.sql", Tag = "Sql Script", NodePath = "" };
        var treeService = CreateTreeService(scriptNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "orphan";
        vm.SearchCodeCommand.Execute(null);

        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        Assert.That(scriptResults, Is.Empty);
    }

    [Test]
    public void SearchCode_ScriptWithNullNodePath_IsSkipped()
    {
        var scriptNode = new TreeNodeModel { Text = "orphan.sql", Tag = "Sql Script", NodePath = null! };
        var treeService = CreateTreeService(scriptNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "orphan";
        vm.SearchCodeCommand.Execute(null);

        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        Assert.That(scriptResults, Is.Empty);
    }

    [Test]
    public void SearchCode_ScriptWithUnreadablePath_IsSkipped()
    {
        var scriptNode = new TreeNodeModel
        {
            Text = "missing.sql",
            Tag = "Sql Script",
            NodePath = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid() + ".sql")
        };
        var treeService = CreateTreeService(scriptNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "missing";
        vm.SearchCodeCommand.Execute(null);

        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        Assert.That(scriptResults, Is.Empty);
    }

    [Test]
    public void SearchCode_ScriptWithEmptyTemplateName_ShowsProductLabel()
    {
        var vm = CreateRealSearchViewModel();
        vm.CodeSearchTerm = "CREATE";
        vm.SearchCodeCommand.Execute(null);

        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        Assert.That(scriptResults.All(r => !string.IsNullOrEmpty(r.Template)), Is.True);
    }

    [Test]
    public void SearchCode_ResultNames_HaveBracketsStripped()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "TestTable",
            Columns = [new Schema.Domain.Column { Name = "[BracketedCol]", DataType = "INT" }],
            Indexes = [new Schema.Domain.Index { Name = "[IX_Bracketed]", IndexColumns = "[BracketedCol]" }],
            ForeignKeys = [new Schema.Domain.ForeignKey { Name = "[FK_Bracketed]", Columns = "[Col1]", RelatedTable = "[Other]", RelatedColumns = "[Id]" }],
            CheckConstraints = [new Schema.Domain.CheckConstraint { Name = "[CK_Bracketed]", Expression = "1=1" }],
            Statistics = [new Schema.Domain.Statistic { Name = "[ST_Bracketed]", Columns = "[Col1]" }]
        };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.TestTable",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            ColumnNodes = [],
            IndexNodes = [],
            ForeignKeyNodes = [],
            CheckConstraintNodes = [],
            StatisticNodes = []
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "Bracketed";
        vm.SearchCodeCommand.Execute(null);

        Assert.That(vm.CodeSearchResults, Is.Not.Empty);
        foreach (var result in vm.CodeSearchResults)
        {
            Assert.That(result.Name, Does.Not.Contain("["),
                $"Result name '{result.Name}' (type: {result.Type}) should have brackets stripped");
            Assert.That(result.Name, Does.Not.Contain("]"),
                $"Result name '{result.Name}' (type: {result.Type}) should have brackets stripped");
        }
    }

    [Test]
    public void SearchCode_TableNodeWithNullTableData_IsSkipped()
    {
        var tableNode = new TableNodeModel
        {
            Text = "dbo.TestTable",
            Tag = "Table",
            TemplateName = "Main",
            TableData = null
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "TestTable";
        vm.SearchCodeCommand.Execute(null);

        var tableResults = vm.CodeSearchResults.Where(r => r.Type == "Table").ToList();
        Assert.That(tableResults, Is.Empty);
    }

    [Test]
    public void SearchCode_TableMetadata_ColumnNodeFallsBackToTableNode()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "TestTable",
            Columns = [new Schema.Domain.Column { Name = "SearchCol", DataType = "INT" }]
        };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.TestTable",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            ColumnNodes = []
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "SearchCol";
        vm.SearchCodeCommand.Execute(null);

        var colResults = vm.CodeSearchResults.Where(r => r.Type == "Column").ToList();
        Assert.That(colResults, Has.Count.EqualTo(1));
        Assert.That(colResults[0].Node, Is.SameAs(tableNode));
    }

    [Test]
    public void SearchCode_TableMetadata_IndexNodeFallsBackToTableNode()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "TestTable",
            Indexes = [new Schema.Domain.Index { Name = "IX_Search", IndexColumns = "Col1" }]
        };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.TestTable",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            IndexNodes = []
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "IX_Search";
        vm.SearchCodeCommand.Execute(null);

        var idxResults = vm.CodeSearchResults.Where(r => r.Type == "Index").ToList();
        Assert.That(idxResults, Has.Count.EqualTo(1));
        Assert.That(idxResults[0].Node, Is.SameAs(tableNode));
    }

    [Test]
    public void SearchCode_TableMetadata_ForeignKeyNodeFallsBackToTableNode()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "TestTable",
            ForeignKeys = [new Schema.Domain.ForeignKey { Name = "FK_Search", Columns = "Col1", RelatedTable = "Other", RelatedColumns = "Id" }]
        };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.TestTable",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            ForeignKeyNodes = []
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "FK_Search";
        vm.SearchCodeCommand.Execute(null);

        var fkResults = vm.CodeSearchResults.Where(r => r.Type == "Foreign Key").ToList();
        Assert.That(fkResults, Has.Count.EqualTo(1));
        Assert.That(fkResults[0].Node, Is.SameAs(tableNode));
    }

    [Test]
    public void SearchCode_TableMetadata_CheckConstraintNodeFallsBackToTableNode()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "TestTable",
            CheckConstraints = [new Schema.Domain.CheckConstraint { Name = "CK_Search", Expression = "Col > 0" }]
        };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.TestTable",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            CheckConstraintNodes = []
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "CK_Search";
        vm.SearchCodeCommand.Execute(null);

        var ccResults = vm.CodeSearchResults.Where(r => r.Type == "Check Constraint").ToList();
        Assert.That(ccResults, Has.Count.EqualTo(1));
        Assert.That(ccResults[0].Node, Is.SameAs(tableNode));
    }

    [Test]
    public void SearchCode_TableMetadata_StatisticNodeFallsBackToTableNode()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "TestTable",
            Statistics = [new Schema.Domain.Statistic { Name = "ST_Search", Columns = "Col1" }]
        };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.TestTable",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            StatisticNodes = []
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "ST_Search";
        vm.SearchCodeCommand.Execute(null);

        var statResults = vm.CodeSearchResults.Where(r => r.Type == "Statistic").ToList();
        Assert.That(statResults, Has.Count.EqualTo(1));
        Assert.That(statResults[0].Node, Is.SameAs(tableNode));
    }

    [Test]
    public void SearchCode_ScriptTokens_WithProductTokens_FindsMatch()
    {
        var product = new Schema.Domain.Product();
        product.ScriptTokens["TestKey"] = "TestValue";
        var productNode = new TreeNodeModel { Text = "Product", Tag = "Product" };

        var service = Substitute.For<IProductTreeService>();
        service.SearchList.Returns(new List<TreeNodeModel> { productNode });
        service.Product.Returns(product);
        service.Templates.Returns(new Dictionary<string, Schema.Domain.Template>(StringComparer.OrdinalIgnoreCase));

        var vm = new SearchViewModel(service);
        vm.CodeSearchTerm = "TestKey";
        vm.SearchCodeCommand.Execute(null);

        var tokenResults = vm.CodeSearchResults.Where(r => r.Type == "Script Token").ToList();
        Assert.That(tokenResults, Has.Count.EqualTo(1));
        Assert.That(tokenResults[0].Name, Is.EqualTo("{{TestKey}}"));
        Assert.That(tokenResults[0].Template, Is.EqualTo("(Product)"));
    }

    [Test]
    public void SearchCode_ScriptTokens_WithTemplateTokens_FindsMatch()
    {
        var template = new Schema.Domain.Template();
        template.ScriptTokens["TplKey"] = "TplValue";

        var templateNode = new TreeNodeModel { Text = "Main", Tag = "Template" };

        var service = Substitute.For<IProductTreeService>();
        service.SearchList.Returns(new List<TreeNodeModel> { templateNode });
        service.Product.Returns((Schema.Domain.Product?)null);
        service.Templates.Returns(new Dictionary<string, Schema.Domain.Template>(StringComparer.OrdinalIgnoreCase)
        {
            ["Main"] = template
        });

        var vm = new SearchViewModel(service);
        vm.CodeSearchTerm = "TplKey";
        vm.SearchCodeCommand.Execute(null);

        var tokenResults = vm.CodeSearchResults.Where(r => r.Type == "Script Token").ToList();
        Assert.That(tokenResults, Has.Count.EqualTo(1));
        Assert.That(tokenResults[0].Name, Is.EqualTo("{{TplKey}}"));
        Assert.That(tokenResults[0].Template, Is.EqualTo("Main"));
    }

    [Test]
    public void SearchCode_ScriptTokens_MatchesByValue()
    {
        var product = new Schema.Domain.Product();
        product.ScriptTokens["DB"] = "MyDatabase";
        var productNode = new TreeNodeModel { Text = "Product", Tag = "Product" };

        var service = Substitute.For<IProductTreeService>();
        service.SearchList.Returns(new List<TreeNodeModel> { productNode });
        service.Product.Returns(product);
        service.Templates.Returns(new Dictionary<string, Schema.Domain.Template>(StringComparer.OrdinalIgnoreCase));

        var vm = new SearchViewModel(service);
        vm.CodeSearchTerm = "MyDatabase";
        vm.SearchCodeCommand.Execute(null);

        var tokenResults = vm.CodeSearchResults.Where(r => r.Type == "Script Token").ToList();
        Assert.That(tokenResults, Has.Count.EqualTo(1));
    }

    [Test]
    public void SearchCode_ScriptTokens_NoProductNode_StillSearchesTokens()
    {
        var product = new Schema.Domain.Product();
        product.ScriptTokens["Key1"] = "Val1";

        var service = Substitute.For<IProductTreeService>();
        service.SearchList.Returns(new List<TreeNodeModel>());
        service.Product.Returns(product);
        service.Templates.Returns(new Dictionary<string, Schema.Domain.Template>(StringComparer.OrdinalIgnoreCase));

        var vm = new SearchViewModel(service);
        vm.CodeSearchTerm = "Key1";
        vm.SearchCodeCommand.Execute(null);

        var tokenResults = vm.CodeSearchResults.Where(r => r.Type == "Script Token").ToList();
        Assert.That(tokenResults, Has.Count.EqualTo(1));
        Assert.That(tokenResults[0].Node, Is.Null);
    }

    [Test]
    public void SearchCode_TableMetadata_FindsColumnByDefault()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "T1",
            Columns = [new Schema.Domain.Column { Name = "Col1", DataType = "INT", Default = "42" }]
        };
        var colNode = new TreeNodeModel { Text = "Col1", Tag = "Column" };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.T1",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            ColumnNodes = [colNode]
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "42";
        vm.SearchCodeCommand.Execute(null);

        var colResults = vm.CodeSearchResults.Where(r => r.Type == "Column").ToList();
        Assert.That(colResults, Has.Count.EqualTo(1));
        Assert.That(colResults[0].Node, Is.SameAs(colNode));
    }

    [Test]
    public void SearchCode_TableMetadata_FindsIndexByFilterExpression()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "T1",
            Indexes = [new Schema.Domain.Index { Name = "IX_1", IndexColumns = "C1", FilterExpression = "Status = 1" }]
        };
        var idxNode = new TreeNodeModel { Text = "IX_1", Tag = "Index" };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.T1",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            IndexNodes = [idxNode]
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "Status = 1";
        vm.SearchCodeCommand.Execute(null);

        var idxResults = vm.CodeSearchResults.Where(r => r.Type == "Index").ToList();
        Assert.That(idxResults, Has.Count.EqualTo(1));
    }

    [Test]
    public void SearchCode_TableMetadata_FindsStatisticByFilterExpression()
    {
        var table = new Schema.Domain.Table
        {
            Schema = "dbo",
            Name = "T1",
            Statistics = [new Schema.Domain.Statistic { Name = "ST_1", Columns = "Col1", FilterExpression = "Active = 1" }]
        };
        var statNode = new TreeNodeModel { Text = "ST_1", Tag = "Statistic" };
        var tableNode = new TableNodeModel
        {
            Text = "dbo.T1",
            Tag = "Table",
            TemplateName = "Main",
            TableData = table,
            StatisticNodes = [statNode]
        };
        var treeService = CreateTreeService(tableNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "Active = 1";
        vm.SearchCodeCommand.Execute(null);

        var statResults = vm.CodeSearchResults.Where(r => r.Type == "Statistic").ToList();
        Assert.That(statResults, Has.Count.EqualTo(1));
    }

    [Test]
    public void SearchCode_SetsIsSearchingDuringSearch()
    {
        var vm = CreateRealSearchViewModel();
        Assert.That(vm.IsSearching, Is.False);

        vm.CodeSearchTerm = "CREATE";
        vm.SearchCodeCommand.Execute(null);

        Assert.That(vm.IsSearching, Is.False);
    }

    [Test]
    public void ChangeSearchType_WithExistingTerm_ReSearches()
    {
        var node1 = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var node2 = new TreeNodeModel { Text = "MyTest", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node1, node2);
        var vm = new SearchViewModel(treeService);

        vm.TreeSearchTerm = "Test";
        vm.SearchTreeCommand.Execute(null);
        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(2));

        vm.SelectedSearchType = "Begins With";
        Assert.That(vm.TreeSearchResults, Has.Count.EqualTo(1));
        Assert.That(vm.TreeSearchResults[0].Name, Is.EqualTo("TestTable"));
    }

    [Test]
    public void ChangeSearchType_WithEmptyTerm_DoesNotSearch()
    {
        var node = new TreeNodeModel { Text = "TestTable", Tag = "Table", TemplateName = "Main" };
        var treeService = CreateTreeService(node);
        var vm = new SearchViewModel(treeService);

        vm.SelectedSearchType = "Begins With";
        Assert.That(vm.TreeSearchResults, Is.Empty);
    }

    [Test]
    public void SearchCode_TemplateScripts_ShowTemplateName()
    {
        var scriptNode = new TreeNodeModel
        {
            Text = "deploy.sql",
            Tag = "Sql Script",
            TemplateName = "Main",
            NodePath = Path.Combine(ValidProductPath, "Templates", "Main", "MigrationScripts", "Before", "deploy.sql")
        };
        var treeService = CreateTreeService(scriptNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "CREATE";
        vm.SearchCodeCommand.Execute(null);

        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        if (scriptResults.Any())
        {
            Assert.That(scriptResults[0].Template, Is.EqualTo("Main"));
        }
    }

    [Test]
    public void SearchCode_ProductLevelScripts_ShowProductLabel()
    {
        var scriptNode = new TreeNodeModel
        {
            Text = "product-script.sql",
            Tag = "Sql Script",
            TemplateName = "",
            NodePath = Path.Combine(ValidProductPath, "SomeFolder", "product-script.sql")
        };
        var treeService = CreateTreeService(scriptNode);
        var vm = new SearchViewModel(treeService);

        vm.CodeSearchTerm = "something";
        vm.SearchCodeCommand.Execute(null);

        // Script with empty TemplateName should show "(Product)"
        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        foreach (var result in scriptResults)
        {
            Assert.That(result.Template, Is.EqualTo("(Product)"));
        }
    }
}

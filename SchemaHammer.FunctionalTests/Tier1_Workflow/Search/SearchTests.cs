// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: none — Community-only search tests.

using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;

namespace SchemaHammer.FunctionalTests.Tier1_Workflow.Search;

[TestFixture]
public class SearchTests : TestProductFixture
{
    private ProductTreeService _treeService = null!;
    private SearchViewModel _searchVm = null!;

    [SetUp]
    public new void SetUp()
    {
        base.SetUp();

        new TestProductBuilder()
            .WithName("SearchTestProduct")
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Users]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithColumn("[Name]", "nvarchar(100)")
                    .WithColumn("[Email]", "nvarchar(255)")
                    .WithColumn("[Status]", "nvarchar(50)", nullable: true, defaultValue: "'Active'")
                    .WithIndex("[IX_Users_Email]", "[Email]", unique: true)
                    .WithForeignKey("[FK_Users_Roles]", "[RoleId]", "[dbo].[Roles]", "[Id]"))
                .WithTable("[dbo].[Orders]", table => table
                    .WithColumn("[OrderId]", "int", nullable: false)
                    .WithColumn("[UserId]", "int", nullable: false)
                    .WithColumn("[OrderDate]", "datetime2")
                    .WithForeignKey("[FK_Orders_Users]", "[UserId]", "[dbo].[Users]", "[Id]"))
                .WithTable("[dbo].[Products]", table => table
                    .WithColumn("[ProductId]", "int", nullable: false)
                    .WithColumn("[ProductName]", "nvarchar(200)")
                    .WithIndex("[IX_Products_Name]", "[ProductName]"))
                .WithScriptFolder("Views")
                .WithScript("Views", "vw_ActiveUsers.sql",
                    "SELECT Id, Name FROM Users WHERE Status = 'Active'")
                .WithScript("Views", "vw_OrderSummary.sql",
                    "SELECT OrderId, UserId FROM Orders"))
            .Build(TempDir);

        _treeService = new ProductTreeService();
        _treeService.LoadProduct(TempDir);
        _searchVm = new SearchViewModel(_treeService);

        // Expand tables so SearchList has full data
        foreach (var node in _treeService.SearchList.ToList())
            node.EnsureExpanded();
    }

    // --- Tree Search Tests ---

    [Test]
    public void TreeSearch_Contains_FindsMatchingNodes()
    {
        _searchVm.SelectedSearchType = "Contains";
        _searchVm.TreeSearchTerm = "Users";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Not.Empty, "Should find nodes containing 'Users'");
    }

    [Test]
    public void TreeSearch_BeginsWith_FindsMatchingNodes()
    {
        _searchVm.SelectedSearchType = "Begins With";
        _searchVm.TreeSearchTerm = "[dbo]";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Not.Empty, "Should find nodes beginning with '[dbo]'");
    }

    [Test]
    public void TreeSearch_EndsWith_FindsMatchingNodes()
    {
        _searchVm.SelectedSearchType = "Ends With";
        _searchVm.TreeSearchTerm = "Orders]";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Not.Empty, "Should find nodes ending with 'Orders]'");
    }

    [Test]
    public void TreeSearch_IsCaseInsensitive()
    {
        _searchVm.SelectedSearchType = "Contains";
        _searchVm.TreeSearchTerm = "users";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Not.Empty,
            "Search should be case-insensitive");
    }

    [Test]
    public void TreeSearch_EmptyTerm_ReturnsEmpty()
    {
        _searchVm.TreeSearchTerm = "";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Empty,
            "Empty search term should return no results");
    }

    [Test]
    public void TreeSearch_NoMatch_ReturnsEmpty()
    {
        _searchVm.TreeSearchTerm = "ZZZNONEXISTENTZZZ";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Empty,
            "Non-matching term should return no results");
    }

    [Test]
    public void TreeSearch_ResultsHaveValidData()
    {
        _searchVm.TreeSearchTerm = "Users";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Not.Empty);
        foreach (var result in _searchVm.TreeSearchResults)
        {
            Assert.That(result.Name, Is.Not.Null.And.Not.Empty, "Result Name should not be empty");
            Assert.That(result.Node, Is.Not.Null, "Result Node should not be null");
        }
    }

    [Test]
    public void TreeSearch_ResultsIncludeTemplateName()
    {
        // Search for a table name — table nodes have TemplateName set
        _searchVm.TreeSearchTerm = "[dbo].[Users]";
        _searchVm.SearchTreeCommand.Execute(null);

        Assert.That(_searchVm.TreeSearchResults, Is.Not.Empty);
        var result = _searchVm.TreeSearchResults.First();
        Assert.That(result.Template, Is.Not.Null.And.Not.Empty,
            "Table node search result should include a template name");
    }

    [Test]
    public void TreeSearch_ContainerNodesAreFiltered()
    {
        // Container nodes like "Tables", "Indexes Container", etc. should be excluded from results
        _searchVm.TreeSearchTerm = "Main";
        _searchVm.SearchTreeCommand.Execute(null);

        // Template node "Main" should appear; container nodes like "Tables" should not
        foreach (var result in _searchVm.TreeSearchResults)
        {
            Assert.That(result.Type, Does.Not.Contain("Container").IgnoreCase,
                $"Container nodes should be filtered from results, but got type '{result.Type}'");
        }
    }

    // --- Code Search Tests ---

    [Test]
    public void CodeSearch_FindsTextInScripts()
    {
        _searchVm.CodeSearchTerm = "Active";
        _searchVm.SearchCodeCommand.Execute(null);

        Assert.That(_searchVm.CodeSearchResults, Is.Not.Empty,
            "Should find script containing 'Active'");
        Assert.That(_searchVm.CodeSearchResults.Any(r => r.Type == "Sql Script"), Is.True,
            "Should find a Sql Script result for text in script content");
    }

    [Test]
    public void CodeSearch_EmptyTerm_ReturnsEmpty()
    {
        _searchVm.CodeSearchTerm = "";
        _searchVm.SearchCodeCommand.Execute(null);

        Assert.That(_searchVm.CodeSearchResults, Is.Empty,
            "Empty code search term should return no results");
    }

    [Test]
    public void CodeSearch_FindsForeignKeyByRelatedTableName()
    {
        _searchVm.CodeSearchTerm = "dbo].[Users]";
        _searchVm.SearchCodeCommand.Execute(null);

        var fkResults = _searchVm.CodeSearchResults
            .Where(r => r.Type == "Foreign Key")
            .ToList();

        Assert.That(fkResults, Is.Not.Empty,
            "Should find foreign key by related table name '[dbo].[Users]'");
    }

    [Test]
    public void CodeSearch_FindsIndexByColumnName()
    {
        _searchVm.CodeSearchTerm = "Email";
        _searchVm.SearchCodeCommand.Execute(null);

        var indexResults = _searchVm.CodeSearchResults
            .Where(r => r.Type == "Index")
            .ToList();

        Assert.That(indexResults, Is.Not.Empty,
            "Should find index that references 'Email' column");
    }

    [Test]
    public void CodeSearch_FindsColumnByDefaultValue()
    {
        _searchVm.CodeSearchTerm = "Active";
        _searchVm.SearchCodeCommand.Execute(null);

        var colResults = _searchVm.CodeSearchResults
            .Where(r => r.Type == "Column")
            .ToList();

        Assert.That(colResults, Is.Not.Empty,
            "Should find column with default value containing 'Active'");
    }

    [Test]
    public void CodeSearch_NoResults_ReturnsEmpty()
    {
        _searchVm.CodeSearchTerm = "ZZZNONEXISTENTZZZ";
        _searchVm.SearchCodeCommand.Execute(null);

        Assert.That(_searchVm.CodeSearchResults, Is.Empty,
            "Non-matching code search should return no results");
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: none — Community-only tree navigation tests.

using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.FunctionalTests.Tier1_Workflow.Navigation;

[TestFixture]
public class TreeNavigationTests : TestProductFixture
{
    private ProductTreeService _treeService = null!;
    private EditorService _editorService = null!;
    private List<SchemaHammer.Models.TreeNodeModel> _roots = null!;

    [SetUp]
    public new void SetUp()
    {
        base.SetUp();
        BuildStandardSqlServerProduct();
        _treeService = new ProductTreeService();
        _roots = _treeService.LoadProduct(TempDir);
        _editorService = new EditorService();
    }

    [Test]
    public void Tree_HasTemplatesContainerInRoots()
    {
        var templatesNode = _roots.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesNode, Is.Not.Null, "Templates container should be in tree roots");
        Assert.That(templatesNode!.Text, Is.EqualTo("Templates"));
    }

    [Test]
    public void TemplatesContainer_HasMainTemplate()
    {
        var node = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main");
        Assert.That(node.Tag, Is.EqualTo("Template"));
    }

    [Test]
    public void MainTemplate_HasTablesContainer()
    {
        var node = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Tables");
        Assert.That(node.Tag, Is.EqualTo("Tables"));
    }

    [Test]
    public void TablesContainer_HasUsersAndOrders()
    {
        var tablesNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Tables");
        tablesNode.EnsureExpanded();
        AssertionHelpers.AssertChildrenContain(tablesNode, "[dbo].[Users]", "[dbo].[Orders]");
    }

    [Test]
    public void UsersTable_HasColumnsContainerWithIdNameEmail()
    {
        var tableNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Tables/[dbo].[Users]");
        tableNode.EnsureExpanded();

        var columnsContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Columns", StringComparison.OrdinalIgnoreCase));
        Assert.That(columnsContainer, Is.Not.Null, "Columns container not found under Users");

        AssertionHelpers.AssertChildrenContain(columnsContainer!, "Id", "Name", "Email");
    }

    [Test]
    public void UsersTable_HasIndexesContainerWithIX_Users_Email()
    {
        var tableNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Tables/[dbo].[Users]");
        tableNode.EnsureExpanded();

        var indexesContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Indexes", StringComparison.OrdinalIgnoreCase));
        Assert.That(indexesContainer, Is.Not.Null, "Indexes container not found under Users");

        AssertionHelpers.AssertChildrenContain(indexesContainer!, "IX_Users_Email");
    }

    [Test]
    public void OrdersTable_HasForeignKeysContainerWithFK_Orders_Users()
    {
        var tableNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Tables/[dbo].[Orders]");
        tableNode.EnsureExpanded();

        var fkContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Foreign Keys", StringComparison.OrdinalIgnoreCase));
        Assert.That(fkContainer, Is.Not.Null, "Foreign Keys container not found under Orders");

        AssertionHelpers.AssertChildrenContain(fkContainer!, "FK_Orders_Users");
    }

    [Test]
    public void ScriptFolder_Views_ExistsUnderMainTemplate()
    {
        var viewsNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Views");
        Assert.That(viewsNode, Is.Not.Null);
    }

    [Test]
    public void ScriptFile_vw_ActiveUsers_ExistsUnderViewsFolder()
    {
        var scriptNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Views/vw_ActiveUsers.sql");
        Assert.That(scriptNode.Tag, Is.EqualTo("Sql Script"));
    }

    [Test]
    public void EditorService_ReturnsTableEditorViewModel_ForTableNode()
    {
        var tableNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Tables/[dbo].[Users]");
        AssertionHelpers.AssertEditorType<TableEditorViewModel>(_editorService, tableNode);
    }

    [Test]
    public void EditorService_ReturnsSqlScriptEditorViewModel_ForScriptNode()
    {
        var scriptNode = AssertionHelpers.AssertTreeContainsNode(_roots, "Templates/Main/Views/vw_ActiveUsers.sql");
        AssertionHelpers.AssertEditorType<SqlScriptEditorViewModel>(_editorService, scriptNode);
    }

    [Test]
    public void SearchList_IsPopulatedAfterLoadProduct()
    {
        Assert.That(_treeService.SearchList, Is.Not.Empty,
            "SearchList should be populated after LoadProduct");
    }
}

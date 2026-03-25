// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Services;

namespace SchemaHammer.UnitTests;

public class ProductTreeServiceTests
{
    private static readonly string ValidProductPath =
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "TestProducts", "ValidProduct"));

    [Test]
    public void LoadProduct_ReturnsRootNodes()
    {
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        Assert.That(roots, Is.Not.Empty);
    }

    [Test]
    public void LoadProduct_SetsProductProperty()
    {
        var service = new ProductTreeService();
        service.LoadProduct(ValidProductPath);
        Assert.That(service.Product, Is.Not.Null);
        Assert.That(service.Product!.Name, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void LoadProduct_CreatesTemplateNodes()
    {
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesContainer, Is.Not.Null);
    }

    [Test]
    public void LoadProduct_LazyExpandsTemplate_ContainsTablesContainer()
    {
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        templatesContainer!.EnsureExpanded();
        var mainTemplate = templatesContainer.Children.FirstOrDefault(t => t.Text == "Main");
        Assert.That(mainTemplate, Is.Not.Null);
        mainTemplate!.EnsureExpanded();
        var tablesContainer = mainTemplate.Children.FirstOrDefault(c => c.Text == "Tables");
        Assert.That(tablesContainer, Is.Not.Null);
    }

    [Test]
    public void LoadProduct_LazyExpandsTables_CreatesTableNodes()
    {
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        templatesContainer!.EnsureExpanded();
        var mainTemplate = templatesContainer.Children.FirstOrDefault(t => t.Text == "Main");
        mainTemplate!.EnsureExpanded();
        var tablesContainer = mainTemplate.Children.FirstOrDefault(c => c.Text == "Tables");
        tablesContainer!.EnsureExpanded();
        Assert.That(tablesContainer.Children, Is.Not.Empty);
        Assert.That(tablesContainer.Children.All(c => c.Tag == "Table"), Is.True);
    }

    [Test]
    public void LoadProduct_TableNode_HasColumnAndIndexChildren()
    {
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        templatesContainer!.EnsureExpanded();
        var mainTemplate = templatesContainer.Children.FirstOrDefault(t => t.Text == "Main");
        mainTemplate!.EnsureExpanded();
        var tablesContainer = mainTemplate.Children.FirstOrDefault(c => c.Text == "Tables");
        tablesContainer!.EnsureExpanded();
        var table = tablesContainer.Children.First();
        table.EnsureExpanded();
        var columnsContainer = table.Children.FirstOrDefault(c => c.Text == "Columns");
        Assert.That(columnsContainer, Is.Not.Null);
        Assert.That(columnsContainer!.Children, Is.Not.Empty);
    }

    [Test]
    public void LoadProduct_CreatesScriptFolderNodes()
    {
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        templatesContainer!.EnsureExpanded();
        var mainTemplate = templatesContainer.Children.FirstOrDefault(t => t.Text == "Main");
        mainTemplate!.EnsureExpanded();
        var scriptFolders = mainTemplate.Children
            .Where(c => c.Tag.EndsWith("Folder") || c.Tag.EndsWith("FolderContainer"))
            .ToList();
        Assert.That(scriptFolders, Is.Not.Empty);
    }

    [Test]
    public void LoadProduct_SearchList_IsPopulated()
    {
        var service = new ProductTreeService();
        service.LoadProduct(ValidProductPath);
        Assert.That(service.SearchList, Is.Not.Empty);
    }

    [Test]
    public void ReloadProduct_RebuildsTree()
    {
        var service = new ProductTreeService();
        service.LoadProduct(ValidProductPath);
        var firstSearchCount = service.SearchList.Count;
        var roots = service.ReloadProduct();
        Assert.That(roots, Is.Not.Empty);
        Assert.That(service.SearchList.Count, Is.EqualTo(firstSearchCount));
    }

    [Test]
    public void LoadProduct_IndexedViews_WhenPresent()
    {
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        templatesContainer!.EnsureExpanded();
        var mainTemplate = templatesContainer.Children.FirstOrDefault(t => t.Text == "Main");
        mainTemplate!.EnsureExpanded();
        var ivContainer = mainTemplate.Children.FirstOrDefault(c => c.Text == "Indexed Views");
        if (ivContainer != null)
        {
            ivContainer.EnsureExpanded();
            Assert.That(ivContainer.Children, Is.Not.Empty);
            Assert.That(ivContainer.Children.All(c => c.Tag == "Indexed View"), Is.True);
        }
    }

    [Test]
    public void LoadProduct_PopulatesTemplatesDictionary()
    {
        var service = new ProductTreeService();
        service.LoadProduct(ValidProductPath);
        Assert.That(service.Templates, Is.Not.Empty);
        Assert.That(service.Templates.ContainsKey("Main"), Is.True);
        Assert.That(service.Templates["Main"].Name, Is.EqualTo("Main"));
    }

    [Test]
    public void LoadProduct_TemplatesDictionary_ContainsAllTemplates()
    {
        var service = new ProductTreeService();
        service.LoadProduct(ValidProductPath);
        // ValidProduct has Main, Secondary, Bogus templates
        Assert.That(service.Templates.Keys, Is.SupersetOf(new[] { "Main", "Secondary" }));
    }

    [Test]
    public void ReloadProduct_ClearsAndRepopulatesTemplates()
    {
        var service = new ProductTreeService();
        service.LoadProduct(ValidProductPath);
        var firstCount = service.Templates.Count;
        service.ReloadProduct();
        Assert.That(service.Templates.Count, Is.EqualTo(firstCount));
    }

    [Test]
    public void ReloadProduct_WithNoProduct_ReturnsEmpty()
    {
        var service = new ProductTreeService();
        // Never loaded — should return empty
        var result = service.ReloadProduct();
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void LoadProduct_SqlErrorFiles_AppearInFolderWithErrorTag()
    {
        // ValidProduct/Templates/Main/Functions contains dbo.FailedFunction.sqlerror
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        templatesContainer!.EnsureExpanded();
        var mainTemplate = templatesContainer.Children.FirstOrDefault(t => t.Text == "Main");
        mainTemplate!.EnsureExpanded();
        var functionsFolder = mainTemplate.Children.FirstOrDefault(c => c.Text == "Functions");
        Assert.That(functionsFolder, Is.Not.Null, "Functions folder should exist");
        functionsFolder!.EnsureExpanded();

        var errorNode = functionsFolder.Children.FirstOrDefault(c =>
            c.Text.EndsWith(".sqlerror", StringComparison.OrdinalIgnoreCase));

        Assert.That(errorNode, Is.Not.Null, "A .sqlerror node should be visible in the Functions folder");
        Assert.That(errorNode!.Tag, Is.EqualTo("Sql Error Script"));
        Assert.That(errorNode.ImageKey, Is.EqualTo("error-file"));
    }

    [Test]
    public void LoadProduct_SqlErrorFolderNode_IncludesBothSqlAndSqlErrorFiles()
    {
        // Functions folder has both .sql and .sqlerror files
        var service = new ProductTreeService();
        var roots = service.LoadProduct(ValidProductPath);
        var templatesContainer = roots.FirstOrDefault(n => n.Tag == "Templates");
        templatesContainer!.EnsureExpanded();
        var mainTemplate = templatesContainer.Children.FirstOrDefault(t => t.Text == "Main");
        mainTemplate!.EnsureExpanded();
        var functionsFolder = mainTemplate.Children.FirstOrDefault(c => c.Text == "Functions");
        Assert.That(functionsFolder, Is.Not.Null);
        functionsFolder!.EnsureExpanded();

        var sqlNodes = functionsFolder.Children.Where(c => c.Tag == "Sql Script").ToList();
        var errorNodes = functionsFolder.Children.Where(c => c.Tag == "Sql Error Script").ToList();

        Assert.That(sqlNodes, Is.Not.Empty, "Regular .sql nodes should be present");
        Assert.That(errorNodes, Is.Not.Empty, ".sqlerror nodes should be present");
    }
}

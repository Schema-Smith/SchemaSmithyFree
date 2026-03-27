// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

public abstract class RenderingTestBase : TestProductFixture
{
    protected ProductTreeService TreeService { get; private set; } = null!;
    protected EditorService EditorService { get; private set; } = null!;
    protected List<TreeNodeModel> TreeRoots { get; private set; } = [];

    [SetUp]
    public new void SetUp()
    {
        base.SetUp();
        BuildStandardSqlServerProduct();

        TreeService = new ProductTreeService();
        // LoadProduct takes the product directory, not the .json file path
        TreeRoots = TreeService.LoadProduct(TempDir);
        EditorService = new EditorService();
    }

    protected static Window HostView(Control view)
    {
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    protected static T? FindControl<T>(Control parent, string name) where T : Control
        => parent.FindControl<T>(name);

    protected static T? FindDescendant<T>(Control parent) where T : Control
        => parent.GetVisualDescendants().OfType<T>().FirstOrDefault();

    protected static List<T> FindDescendants<T>(Control parent) where T : Control
        => parent.GetVisualDescendants().OfType<T>().ToList();

    // The tree structure for the standard product:
    //   roots → [Templates (container)]
    //   Templates → expand → [Main (template)]
    //   Main → expand → [Views (script folder), Tables (container)]
    //   Tables → expand → [[dbo].[Users], [dbo].[Orders]]
    //   [dbo].[Users] → expand → [Columns (container), Indexes (container)]
    //   Columns → [Id, Name, Email]

    protected TableEditorViewModel GetTableEditor()
    {
        var node = FindTableNode("Users");
        Assert.That(node, Is.Not.Null, "Could not find Users table in tree");
        return AssertionHelpers.AssertEditorType<TableEditorViewModel>(EditorService, node!);
    }

    protected ColumnEditorViewModel GetColumnEditor()
    {
        var tableNode = FindTableNode("Users");
        Assert.That(tableNode, Is.Not.Null, "Could not find Users table in tree");
        tableNode!.EnsureExpanded();

        var columnsContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Columns", StringComparison.OrdinalIgnoreCase));
        Assert.That(columnsContainer, Is.Not.Null, "Columns container not found under Users");

        var colNode = columnsContainer!.Children.FirstOrDefault(c =>
            c.Text.Contains("Id", StringComparison.OrdinalIgnoreCase));
        Assert.That(colNode, Is.Not.Null, "Column 'Id' not found under Columns");
        return AssertionHelpers.AssertEditorType<ColumnEditorViewModel>(EditorService, colNode!);
    }

    protected IndexEditorViewModel GetIndexEditor()
    {
        var tableNode = FindTableNode("Users");
        Assert.That(tableNode, Is.Not.Null, "Could not find Users table in tree");
        tableNode!.EnsureExpanded();

        var indexesContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Indexes", StringComparison.OrdinalIgnoreCase));
        Assert.That(indexesContainer, Is.Not.Null, "Indexes container not found under Users");

        var indexNode = indexesContainer!.Children.FirstOrDefault(c =>
            c.Text.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase));
        Assert.That(indexNode, Is.Not.Null, "Index 'IX_Users_Email' not found under Indexes");
        return AssertionHelpers.AssertEditorType<IndexEditorViewModel>(EditorService, indexNode!);
    }

    protected ForeignKeyEditorViewModel GetForeignKeyEditor()
    {
        var tableNode = FindTableNode("Orders");
        Assert.That(tableNode, Is.Not.Null, "Could not find Orders table in tree");
        tableNode!.EnsureExpanded();

        var fkContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Foreign Keys", StringComparison.OrdinalIgnoreCase));
        Assert.That(fkContainer, Is.Not.Null, "Foreign Keys container not found under Orders");

        var fkNode = fkContainer!.Children.FirstOrDefault(c =>
            c.Text.Contains("FK_Orders_Users", StringComparison.OrdinalIgnoreCase));
        Assert.That(fkNode, Is.Not.Null, "FK 'FK_Orders_Users' not found under Foreign Keys");
        return AssertionHelpers.AssertEditorType<ForeignKeyEditorViewModel>(EditorService, fkNode!);
    }

    protected ProductEditorViewModel GetProductEditor()
    {
        // There is no product root node — the product is available via TreeService.Product.
        // The nearest equivalent is the Templates container node.
        // For a product editor node, use the Templates container since there is no dedicated product node.
        // If tests need a product editor, use TreeService.Product directly or use the Templates tag.
        // The EditorService maps Tag="Templates" → ContainerEditorViewModel, not ProductEditorViewModel.
        // Since there is no product node in the Community tree, we create a synthetic one for the editor.
        var productNode = new TreeNodeModel { Text = Product!.Name, Tag = "Product", NodePath = Product.FilePath };
        return AssertionHelpers.AssertEditorType<ProductEditorViewModel>(EditorService, productNode);
    }

    protected TemplateEditorViewModel GetTemplateEditor()
    {
        var templatesContainer = TreeRoots.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesContainer, Is.Not.Null, "Templates container not found in tree roots");
        templatesContainer!.EnsureExpanded();

        var templateNode = templatesContainer.Children
            .FirstOrDefault(n => n.Text.Equals("Main", StringComparison.OrdinalIgnoreCase));
        Assert.That(templateNode, Is.Not.Null, "Template 'Main' not found");
        return AssertionHelpers.AssertEditorType<TemplateEditorViewModel>(EditorService, templateNode!);
    }

    protected ContainerEditorViewModel GetContainerEditor()
    {
        var templatesContainer = TreeRoots.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesContainer, Is.Not.Null, "Templates container not found");
        templatesContainer!.EnsureExpanded();

        var mainTemplate = templatesContainer.Children
            .FirstOrDefault(n => n.Text.Equals("Main", StringComparison.OrdinalIgnoreCase));
        Assert.That(mainTemplate, Is.Not.Null, "Template 'Main' not found");
        mainTemplate!.EnsureExpanded();

        var tablesContainer = mainTemplate.Children
            .FirstOrDefault(c => c.Text.Equals("Tables", StringComparison.OrdinalIgnoreCase));
        Assert.That(tablesContainer, Is.Not.Null, "Tables container not found");
        return AssertionHelpers.AssertEditorType<ContainerEditorViewModel>(EditorService, tablesContainer!);
    }

    protected SqlScriptEditorViewModel GetSqlScriptEditor()
    {
        var scriptNode = TreeRoots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Contains("vw_ActiveUsers", StringComparison.OrdinalIgnoreCase));
        Assert.That(scriptNode, Is.Not.Null, "Script 'vw_ActiveUsers' not found");
        return AssertionHelpers.AssertEditorType<SqlScriptEditorViewModel>(EditorService, scriptNode!);
    }

    private TreeNodeModel? FindTableNode(string tableName)
    {
        var templatesContainer = TreeRoots.FirstOrDefault(n => n.Tag == "Templates");
        if (templatesContainer == null) return null;
        templatesContainer.EnsureExpanded();

        var mainTemplate = templatesContainer.Children
            .FirstOrDefault(n => n.Text.Equals("Main", StringComparison.OrdinalIgnoreCase));
        if (mainTemplate == null) return null;
        mainTemplate.EnsureExpanded();

        var tablesContainer = mainTemplate.Children
            .FirstOrDefault(c => c.Text.Equals("Tables", StringComparison.OrdinalIgnoreCase));
        if (tablesContainer == null) return null;
        tablesContainer.EnsureExpanded();

        return tablesContainer.Children.FirstOrDefault(n =>
            n.Text.Contains(tableName, StringComparison.OrdinalIgnoreCase));
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class EditorServiceTests
{
    [Test]
    public void GetEditorTag_ReturnsContainerForContainerTags()
    {
        var service = new EditorService();
        Assert.That(service.GetEditorTag("Column Container"), Is.EqualTo("Container"));
        Assert.That(service.GetEditorTag("Index Container"), Is.EqualTo("Container"));
        Assert.That(service.GetEditorTag("Before Folder"), Is.EqualTo("Container"));
    }

    [Test]
    public void GetEditorTag_ReturnsTagForKnownTypes()
    {
        var service = new EditorService();
        Assert.That(service.GetEditorTag("Table"), Is.EqualTo("Table"));
        Assert.That(service.GetEditorTag("Column"), Is.EqualTo("Column"));
        Assert.That(service.GetEditorTag("Sql Script"), Is.EqualTo("Sql Script"));
    }

    [Test]
    public void GetEditorTag_ReturnsContainerForGroupTags()
    {
        var service = new EditorService();
        Assert.That(service.GetEditorTag("Templates"), Is.EqualTo("Container"));
        Assert.That(service.GetEditorTag("Tables"), Is.EqualTo("Container"));
        Assert.That(service.GetEditorTag("Indexed Views"), Is.EqualTo("Container"));
    }

    [Test]
    public void GetEditorTag_ReturnsNullForUnknownTags()
    {
        var service = new EditorService();
        Assert.That(service.GetEditorTag("Unknown"), Is.Null);
        Assert.That(service.GetEditorTag(""), Is.Null);
    }

    [Test]
    public void GetEditor_ReturnsTableEditorForTableNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "dbo.Users", Tag = "Table" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<TableEditorViewModel>());
        Assert.That(editor!.Node, Is.SameAs(node));
    }

    [Test]
    public void GetEditor_ReturnsContainerEditorForContainerNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "Columns", Tag = "Column Container" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<ContainerEditorViewModel>());
        Assert.That(editor!.Node, Is.SameAs(node));
    }

    [Test]
    public void GetEditor_CachesByTag_ReusesSameEditorInstance()
    {
        var service = new EditorService();
        var node1 = new TreeNodeModel { Text = "Table1", Tag = "Table" };
        var node2 = new TreeNodeModel { Text = "Table2", Tag = "Table" };

        var editor1 = service.GetEditor(node1);
        var editor2 = service.GetEditor(node2);

        Assert.That(editor2, Is.SameAs(editor1));
        Assert.That(editor2!.Node, Is.SameAs(node2));
    }

    [Test]
    public void GetEditor_DifferentTags_ReturnsDifferentEditors()
    {
        var service = new EditorService();
        var tableNode = new TreeNodeModel { Text = "T1", Tag = "Table" };
        var columnNode = new TreeNodeModel { Text = "C1", Tag = "Column" };

        var tableEditor = service.GetEditor(tableNode);
        var columnEditor = service.GetEditor(columnNode);

        Assert.That(tableEditor, Is.Not.SameAs(columnEditor));
    }

    [Test]
    public void GetEditor_ReturnsColumnEditorForColumnNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "Id", Tag = "Column" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<ColumnEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsIndexEditorForIndexNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "PK_Test", Tag = "Index" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<IndexEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsForeignKeyEditorForFKNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "FK_Test", Tag = "Foreign Key" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<ForeignKeyEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsSqlScriptEditorForScriptNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "deploy.sql", Tag = "Sql Script" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<SqlScriptEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsProductEditorForProductNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "MyProduct", Tag = "Product" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<ProductEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsTemplateEditorForTemplateNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "Main", Tag = "Template" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<TemplateEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsContainerEditorForTemplatesTag()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "Templates", Tag = "Templates" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<ContainerEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsContainerEditorForTablesTag()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "Tables", Tag = "Tables" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<ContainerEditorViewModel>());
    }

    [Test]
    public void GetEditor_ReturnsNullForUnknownTag()
    {
        var service = new EditorService();
        var node = new TreeNodeModel { Text = "Mystery", Tag = "Unknown" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.Null);
    }

    [Test]
    public void GetEditor_ReturnsIndexedViewEditorForIndexedViewNode()
    {
        var service = new EditorService();
        var node = new IndexedViewNodeModel { Text = "dbo.vwTest", Tag = "Indexed View" };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<IndexedViewEditorViewModel>());
    }

    [Test]
    public void GetEditorTag_ReturnsCorrectTagForAllKnownTypes()
    {
        var service = new EditorService();
        Assert.Multiple(() =>
        {
            Assert.That(service.GetEditorTag("Product"), Is.EqualTo("Product"));
            Assert.That(service.GetEditorTag("Template"), Is.EqualTo("Template"));
            Assert.That(service.GetEditorTag("Index"), Is.EqualTo("Index"));
            Assert.That(service.GetEditorTag("Xml Index"), Is.EqualTo("Xml Index"));
            Assert.That(service.GetEditorTag("Foreign Key"), Is.EqualTo("Foreign Key"));
            Assert.That(service.GetEditorTag("Check Constraint"), Is.EqualTo("Check Constraint"));
            Assert.That(service.GetEditorTag("Statistic"), Is.EqualTo("Statistic"));
            Assert.That(service.GetEditorTag("Full Text Index"), Is.EqualTo("Full Text Index"));
            Assert.That(service.GetEditorTag("Indexed View"), Is.EqualTo("Indexed View"));
            Assert.That(service.GetEditorTag(null!), Is.Null);
        });
    }

    [Test]
    public void GetEditorTag_ReturnsContainerForFolderContainerSuffix()
    {
        var service = new EditorService();
        Assert.That(service.GetEditorTag("Script FolderContainer"), Is.EqualTo("Container"));
    }

    [Test]
    public void GetEditorTag_ReturnsSqlScriptForSqlErrorScript()
    {
        var service = new EditorService();
        Assert.That(service.GetEditorTag("Sql Error Script"), Is.EqualTo("Sql Script"));
    }

    [Test]
    public void GetEditor_ReturnsSqlScriptEditorForSqlErrorScriptNode()
    {
        var service = new EditorService();
        var node = new TreeNodeModel
        {
            Text = "dbo.FailedFunction.sqlerror",
            Tag = "Sql Error Script"
        };

        var editor = service.GetEditor(node);

        Assert.That(editor, Is.TypeOf<SqlScriptEditorViewModel>());
    }

    [Test]
    public void GetEditor_SqlErrorScriptNode_SetsIsErrorScriptOnViewModel()
    {
        var service = new EditorService();
        var node = new TreeNodeModel
        {
            Text = "dbo.FailedFunction.sqlerror",
            Tag = "Sql Error Script"
        };

        var editor = service.GetEditor(node) as SqlScriptEditorViewModel;

        Assert.That(editor, Is.Not.Null);
        Assert.That(editor!.IsErrorScript, Is.True);
    }
}

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
}

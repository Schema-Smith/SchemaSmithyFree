using Schema.Domain;
using SchemaHammer.Models;
using DomainIndex = Schema.Domain.Index;

namespace SchemaHammer.UnitTests;

public class TableNodeModelTests
{
    [Test]
    public void ExpandTable_CreatesContainerNodes()
    {
        var table = new Table { Name = "Users", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });
        table.Indexes.Add(new DomainIndex { Name = "PK_Users", IndexColumns = "Id", PrimaryKey = true });

        var node = new TableNodeModel
        {
            Text = "dbo.Users",
            Tag = "Table",
            TableData = table,
            ColumnNodes = [new TreeNodeModel { Text = "Id", Tag = "Column" }],
            IndexNodes = [new TreeNodeModel { Text = "PK_Users", Tag = "Index" }]
        };

        node.ExpandTable();

        Assert.That(node.Children.Any(c => c.Text == "Columns"), Is.True);
        Assert.That(node.Children.Any(c => c.Text == "Indexes"), Is.True);
    }

    [Test]
    public void ExpandTable_SkipsEmptyContainers()
    {
        var table = new Table { Name = "Empty", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var node = new TableNodeModel
        {
            Text = "dbo.Empty",
            Tag = "Table",
            TableData = table,
            ColumnNodes = [new TreeNodeModel { Text = "Id", Tag = "Column" }]
        };

        node.ExpandTable();

        Assert.That(node.Children.Any(c => c.Text == "Indexes"), Is.False);
        Assert.That(node.Children.Any(c => c.Text == "Foreign Keys"), Is.False);
    }

    [Test]
    public void ExpandTable_SetsParentOnContainers()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var node = new TableNodeModel
        {
            Text = "dbo.T",
            Tag = "Table",
            TableData = table,
            ColumnNodes = [new TreeNodeModel { Text = "Id", Tag = "Column" }]
        };

        node.ExpandTable();

        var columnsContainer = node.Children.First(c => c.Text == "Columns");
        Assert.That(columnsContainer.Parent, Is.SameAs(node));
        Assert.That(columnsContainer.Children[0].Parent, Is.SameAs(columnsContainer));
    }

    [Test]
    public void ExpandTable_DoesNotExpandTwice()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var node = new TableNodeModel
        {
            Text = "dbo.T",
            Tag = "Table",
            TableData = table,
            ColumnNodes = [new TreeNodeModel { Text = "Id", Tag = "Column" }]
        };

        node.ExpandTable();
        var firstCount = node.Children.Count;

        node.ExpandTable();

        Assert.That(node.Children.Count, Is.EqualTo(firstCount));
    }

    [Test]
    public void ExpandTable_WithFullTextIndex_CreatesLeafNode()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });
        table.FullTextIndex = new FullTextIndex();

        var node = new TableNodeModel
        {
            Text = "dbo.T",
            Tag = "Table",
            TableData = table,
            ColumnNodes = [new TreeNodeModel { Text = "Id", Tag = "Column" }]
        };

        node.ExpandTable();

        Assert.That(node.Children.Any(c => c.Tag == "Full Text Index"), Is.True);
    }

    [Test]
    public void IndexedViewNodeModel_ExpandIndexedView_CreatesIndexContainer()
    {
        var iv = new IndexedView { Name = "vw_Test", Schema = "dbo", Definition = "SELECT 1" };

        var node = new IndexedViewNodeModel
        {
            Text = "dbo.vw_Test",
            Tag = "Indexed View",
            IndexedViewData = iv,
            IndexNodes = [new TreeNodeModel { Text = "IX_Test", Tag = "Index" }]
        };

        node.ExpandIndexedView();

        var indexContainer = node.Children.FirstOrDefault(c => c.Text == "Indexes");
        Assert.That(indexContainer, Is.Not.Null);
        Assert.That(indexContainer!.Children, Has.Count.EqualTo(1));
    }
}

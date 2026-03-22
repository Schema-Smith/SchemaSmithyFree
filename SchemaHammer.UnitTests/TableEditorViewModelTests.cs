using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;
using DomainIndex = Schema.Domain.Index;

namespace SchemaHammer.UnitTests;

public class TableEditorViewModelTests
{
    [Test]
    public void ChangeNode_LoadsTableProperties()
    {
        var table = new Table { Name = "Users", Schema = "dbo", CompressionType = "PAGE", IsTemporal = true };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var node = new TableNodeModel { Text = "dbo.Users", Tag = "Table", TableData = table };
        var vm = new TableEditorViewModel();
        vm.ChangeNode(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Schema, Is.EqualTo("dbo"));
            Assert.That(vm.Name, Is.EqualTo("Users"));
            Assert.That(vm.CompressionType, Is.EqualTo("PAGE"));
            Assert.That(vm.IsTemporal, Is.True);
        });
    }

    [Test]
    public void ChangeNode_PopulatesSummaryLists()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });
        table.Columns.Add(new Column { Name = "Name", DataType = "nvarchar(100)" });
        table.Indexes.Add(new DomainIndex { Name = "PK_T", IndexColumns = "Id" });

        var node = new TableNodeModel { Text = "dbo.T", Tag = "Table", TableData = table };
        var vm = new TableEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.ColumnSummary, Has.Count.EqualTo(2));
        Assert.That(vm.IndexSummary, Has.Count.EqualTo(1));
        Assert.That(vm.IndexSummary[0], Is.EqualTo("PK_T"));
    }

    [Test]
    public void EditorTitle_ReturnsSchemaQualifiedName()
    {
        var table = new Table { Name = "Users", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var node = new TableNodeModel { Text = "dbo.Users", Tag = "Table", TableData = table };
        var vm = new TableEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.EditorTitle, Is.EqualTo("dbo.Users"));
    }
}

using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class ColumnEditorViewModelTests
{
    [Test]
    public void ChangeNode_LoadsColumnProperties()
    {
        var table = new Table { Name = "Users", Schema = "dbo" };
        table.Columns.Add(new Column
        {
            Name = "Email",
            DataType = "nvarchar(256)",
            Nullable = true,
            Default = "N''",
            Collation = "Latin1_General_CI_AS"
        });

        var tableNode = new TableNodeModel { Text = "dbo.Users", Tag = "Table", TableData = table };
        var container = new TreeNodeModel { Text = "Columns", Tag = "Column Container", Parent = tableNode };
        var columnNode = new TreeNodeModel { Text = "Email", Tag = "Column", Parent = container };

        var vm = new ColumnEditorViewModel();
        vm.ChangeNode(columnNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("Email"));
            Assert.That(vm.DataType, Is.EqualTo("nvarchar(256)"));
            Assert.That(vm.Nullable, Is.True);
            Assert.That(vm.Default, Is.EqualTo("N''"));
            Assert.That(vm.Collation, Is.EqualTo("Latin1_General_CI_AS"));
        });
    }

    [Test]
    public void FindParentTable_WalksUpTree()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var tableNode = new TableNodeModel { Text = "dbo.T", TableData = table };
        var container = new TreeNodeModel { Text = "Columns", Parent = tableNode };
        var columnNode = new TreeNodeModel { Text = "Id", Parent = container };

        var result = ColumnEditorViewModel.FindParentTable(columnNode);
        Assert.That(result, Is.SameAs(table));
    }

    [Test]
    public void EditorTitle_ReturnsColumnName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var tableNode = new TableNodeModel { Text = "dbo.T", TableData = table };
        var container = new TreeNodeModel { Text = "Columns", Parent = tableNode };
        var columnNode = new TreeNodeModel { Text = "Id", Tag = "Column", Parent = container };

        var vm = new ColumnEditorViewModel();
        vm.ChangeNode(columnNode);
        Assert.That(vm.EditorTitle, Is.EqualTo("Id"));
    }
}

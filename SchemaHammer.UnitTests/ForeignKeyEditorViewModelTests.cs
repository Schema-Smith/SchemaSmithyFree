using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class ForeignKeyEditorViewModelTests
{
    private static (TableNodeModel tableNode, TreeNodeModel componentNode) SetupTableTree(
        Table table, string componentText, string containerTag, string componentTag)
    {
        var tableNode = new TableNodeModel { Text = "dbo.T", Tag = "Table", TableData = table };
        var container = new TreeNodeModel { Text = "Foreign Keys", Tag = containerTag, Parent = tableNode };
        var componentNode = new TreeNodeModel { Text = componentText, Tag = componentTag, Parent = container };
        return (tableNode, componentNode);
    }

    [Test]
    public void ChangeNode_LoadsForeignKeyProperties()
    {
        var table = new Table { Name = "Orders", Schema = "dbo" };
        table.ForeignKeys.Add(new ForeignKey
        {
            Name = "[FK_Orders_Customers]",
            Columns = "[CustomerId]",
            RelatedTableSchema = "dbo",
            RelatedTable = "[Customers]",
            RelatedColumns = "[Id]",
            DeleteAction = "CASCADE",
            UpdateAction = "NO ACTION"
        });

        var (_, fkNode) = SetupTableTree(table, "FK_Orders_Customers", "Foreign Key Container", "Foreign Key");

        var vm = new ForeignKeyEditorViewModel();
        vm.ChangeNode(fkNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("FK_Orders_Customers"));
            Assert.That(vm.Columns, Is.EqualTo("[CustomerId]"));
            Assert.That(vm.RelatedTableSchema, Is.EqualTo("dbo"));
            Assert.That(vm.RelatedTable, Is.EqualTo("[Customers]"));
            Assert.That(vm.RelatedColumns, Is.EqualTo("[Id]"));
            Assert.That(vm.DeleteAction, Is.EqualTo("CASCADE"));
            Assert.That(vm.UpdateAction, Is.EqualTo("NO ACTION"));
        });
    }

    [Test]
    public void ChangeNode_StripsDisplayName()
    {
        var table = new Table { Name = "Orders", Schema = "dbo" };
        table.ForeignKeys.Add(new ForeignKey
        {
            Name = "[FK_Orders_Customers]",
            Columns = "CustomerId",
            RelatedTable = "Customers",
            RelatedColumns = "Id"
        });

        var (_, fkNode) = SetupTableTree(table, "FK_Orders_Customers", "Foreign Key Container", "Foreign Key");

        var vm = new ForeignKeyEditorViewModel();
        vm.ChangeNode(fkNode);

        Assert.That(vm.Name, Is.EqualTo("FK_Orders_Customers"));
    }

    [Test]
    public void EditorTitle_ReturnsStrippedName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.ForeignKeys.Add(new ForeignKey
        {
            Name = "[FK_T_Other]",
            Columns = "OtherId",
            RelatedTable = "Other",
            RelatedColumns = "Id"
        });

        var (_, fkNode) = SetupTableTree(table, "FK_T_Other", "Foreign Key Container", "Foreign Key");

        var vm = new ForeignKeyEditorViewModel();
        vm.ChangeNode(fkNode);

        Assert.That(vm.EditorTitle, Is.EqualTo("FK_T_Other"));
    }

    [Test]
    public void ChangeNode_WithNoMatchingFK_LeavesPropertiesEmpty()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        // No foreign keys added

        var (_, fkNode) = SetupTableTree(table, "FK_T_Other", "Foreign Key Container", "Foreign Key");

        var vm = new ForeignKeyEditorViewModel();
        vm.ChangeNode(fkNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo(""));
            Assert.That(vm.Columns, Is.EqualTo(""));
            Assert.That(vm.RelatedTable, Is.EqualTo(""));
            Assert.That(vm.RelatedColumns, Is.EqualTo(""));
            Assert.That(vm.DeleteAction, Is.EqualTo(""));
            Assert.That(vm.UpdateAction, Is.EqualTo(""));
        });
    }
}

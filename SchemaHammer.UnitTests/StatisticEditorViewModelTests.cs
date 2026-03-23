using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class StatisticEditorViewModelTests
{
    private static (TableNodeModel tableNode, TreeNodeModel componentNode) SetupTableTree(
        Table table, string componentText)
    {
        var tableNode = new TableNodeModel { Text = "dbo.T", Tag = "Table", TableData = table };
        var container = new TreeNodeModel { Text = "Statistics", Tag = "Statistic Container", Parent = tableNode };
        var componentNode = new TreeNodeModel { Text = componentText, Tag = "Statistic", Parent = container };
        return (tableNode, componentNode);
    }

    [Test]
    public void ChangeNode_LoadsStatisticProperties()
    {
        var table = new Table { Name = "Orders", Schema = "dbo" };
        table.Statistics.Add(new Statistic
        {
            Name = "[ST_Orders_Amount]",
            Columns = "Amount, CustomerId",
            SampleSize = 50,
            FilterExpression = "[Amount] > 100"
        });

        var (_, statNode) = SetupTableTree(table, "ST_Orders_Amount");

        var vm = new StatisticEditorViewModel();
        vm.ChangeNode(statNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("ST_Orders_Amount"));
            Assert.That(vm.Columns, Is.EqualTo("Amount, CustomerId"));
            Assert.That(vm.SampleSize, Is.EqualTo(50));
            Assert.That(vm.FilterExpression, Is.EqualTo("[Amount] > 100"));
        });
    }

    [Test]
    public void ChangeNode_StripsDisplayName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Statistics.Add(new Statistic
        {
            Name = "[ST_T_Col]",
            Columns = "Col",
            SampleSize = 20
        });

        var (_, statNode) = SetupTableTree(table, "ST_T_Col");

        var vm = new StatisticEditorViewModel();
        vm.ChangeNode(statNode);

        Assert.That(vm.Name, Is.EqualTo("ST_T_Col"));
    }

    [Test]
    public void ChangeNode_WithNoMatchingStat_LeavesPropertiesEmpty()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        // No statistics added

        var (_, statNode) = SetupTableTree(table, "ST_T_Missing");

        var vm = new StatisticEditorViewModel();
        vm.ChangeNode(statNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo(""));
            Assert.That(vm.Columns, Is.EqualTo(""));
            Assert.That(vm.SampleSize, Is.EqualTo(0));
            Assert.That(vm.FilterExpression, Is.EqualTo(""));
        });
    }

    [Test]
    public void EditorTitle_ReturnsStatisticName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Statistics.Add(new Statistic { Name = "[MyStat]", Columns = "Col", SampleSize = 10 });

        var (_, statNode) = SetupTableTree(table, "MyStat");
        var vm = new StatisticEditorViewModel();
        vm.ChangeNode(statNode);

        Assert.That(vm.EditorTitle, Is.Not.Empty);
        Assert.That(vm.EditorTitle, Is.EqualTo("MyStat"));
    }
}

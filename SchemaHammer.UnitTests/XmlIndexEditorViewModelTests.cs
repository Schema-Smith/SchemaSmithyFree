using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class XmlIndexEditorViewModelTests
{
    private static (TableNodeModel tableNode, TreeNodeModel componentNode) SetupTableTree(
        Table table, string componentText)
    {
        var tableNode = new TableNodeModel { Text = "dbo.T", Tag = "Table", TableData = table };
        var container = new TreeNodeModel { Text = "XML Indexes", Tag = "XML Index Container", Parent = tableNode };
        var componentNode = new TreeNodeModel { Text = componentText, Tag = "XML Index", Parent = container };
        return (tableNode, componentNode);
    }

    [Test]
    public void ChangeNode_LoadsXmlIndexProperties()
    {
        var table = new Table { Name = "Orders", Schema = "dbo" };
        table.XmlIndexes.Add(new XmlIndex
        {
            Name = "[PXML_Orders_Details]",
            IsPrimary = true,
            Column = "Details",
            PrimaryIndex = "",
            SecondaryIndexType = ""
        });

        var (_, xiNode) = SetupTableTree(table, "PXML_Orders_Details");

        var vm = new XmlIndexEditorViewModel();
        vm.ChangeNode(xiNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("PXML_Orders_Details"));
            Assert.That(vm.IsPrimary, Is.True);
            Assert.That(vm.Column, Is.EqualTo("Details"));
            Assert.That(vm.PrimaryIndex, Is.EqualTo(""));
            Assert.That(vm.SecondaryIndexType, Is.EqualTo(""));
        });
    }

    [Test]
    public void ChangeNode_LoadsSecondaryXmlIndexProperties()
    {
        var table = new Table { Name = "Orders", Schema = "dbo" };
        table.XmlIndexes.Add(new XmlIndex
        {
            Name = "[SXML_Orders_Details_Path]",
            IsPrimary = false,
            Column = "Details",
            PrimaryIndex = "PXML_Orders_Details",
            SecondaryIndexType = "PATH"
        });

        var (_, xiNode) = SetupTableTree(table, "SXML_Orders_Details_Path");

        var vm = new XmlIndexEditorViewModel();
        vm.ChangeNode(xiNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("SXML_Orders_Details_Path"));
            Assert.That(vm.IsPrimary, Is.False);
            Assert.That(vm.PrimaryIndex, Is.EqualTo("PXML_Orders_Details"));
            Assert.That(vm.SecondaryIndexType, Is.EqualTo("PATH"));
        });
    }

    [Test]
    public void ChangeNode_StripsDisplayName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.XmlIndexes.Add(new XmlIndex
        {
            Name = "[PXML_T_Data]",
            IsPrimary = true,
            Column = "Data"
        });

        var (_, xiNode) = SetupTableTree(table, "PXML_T_Data");

        var vm = new XmlIndexEditorViewModel();
        vm.ChangeNode(xiNode);

        Assert.That(vm.Name, Is.EqualTo("PXML_T_Data"));
    }

    [Test]
    public void ChangeNode_WithNoMatchingXmlIndex_LeavesPropertiesEmpty()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        // No XML indexes added

        var (_, xiNode) = SetupTableTree(table, "PXML_T_Missing");

        var vm = new XmlIndexEditorViewModel();
        vm.ChangeNode(xiNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo(""));
            Assert.That(vm.IsPrimary, Is.False);
            Assert.That(vm.Column, Is.EqualTo(""));
            Assert.That(vm.PrimaryIndex, Is.EqualTo(""));
            Assert.That(vm.SecondaryIndexType, Is.EqualTo(""));
        });
    }
}

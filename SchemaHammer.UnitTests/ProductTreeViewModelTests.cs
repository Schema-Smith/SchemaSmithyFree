using SchemaHammer.Models;
using SchemaHammer.ViewModels;

namespace SchemaHammer.UnitTests;

public class ProductTreeViewModelTests
{
    [Test]
    public void SelectedNode_FiresNodeSelectedEvent()
    {
        var vm = new ProductTreeViewModel();
        TreeNodeModel? eventNode = null;
        vm.NodeSelected += (node, _) => eventNode = node;

        var node = new TreeNodeModel { Text = "Test" };
        vm.SelectedNode = node;

        Assert.That(eventNode, Is.SameAs(node));
    }

    [Test]
    public void SelectedNode_SetsIsSelectedOnNewNode()
    {
        var vm = new ProductTreeViewModel();
        var node = new TreeNodeModel { Text = "Test" };

        vm.SelectedNode = node;

        Assert.That(node.IsSelected, Is.True);
    }

    [Test]
    public void SelectedNode_ClearsIsSelectedOnPreviousNode()
    {
        var vm = new ProductTreeViewModel();
        var nodeA = new TreeNodeModel { Text = "A" };
        var nodeB = new TreeNodeModel { Text = "B" };

        vm.SelectedNode = nodeA;
        vm.SelectedNode = nodeB;

        Assert.That(nodeA.IsSelected, Is.False);
        Assert.That(nodeB.IsSelected, Is.True);
    }

    [Test]
    public void NodeSelected_PassesPreviousNode()
    {
        var vm = new ProductTreeViewModel();
        TreeNodeModel? previousNode = null;
        vm.NodeSelected += (_, prev) => previousNode = prev;

        var nodeA = new TreeNodeModel { Text = "A" };
        var nodeB = new TreeNodeModel { Text = "B" };
        vm.SelectedNode = nodeA;
        vm.SelectedNode = nodeB;

        Assert.That(previousNode, Is.SameAs(nodeA));
    }

    [Test]
    public void SetRootNodes_ClearsAndPopulates()
    {
        var vm = new ProductTreeViewModel();
        vm.RootNodes.Add(new TreeNodeModel { Text = "Old" });

        vm.SetRootNodes([new TreeNodeModel { Text = "New1" }, new TreeNodeModel { Text = "New2" }]);

        Assert.That(vm.RootNodes, Has.Count.EqualTo(2));
        Assert.That(vm.RootNodes[0].Text, Is.EqualTo("New1"));
    }
}

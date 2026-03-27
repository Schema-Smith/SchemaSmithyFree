// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

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

    [Test]
    public void ExpandToNode_SetsParentChainExpanded()
    {
        var vm = new ProductTreeViewModel();
        var root = new TreeNodeModel { Text = "Root" };
        var child = new TreeNodeModel { Text = "Child", Parent = root };
        var grandchild = new TreeNodeModel { Text = "Grandchild", Parent = child };

        vm.ExpandToNode(grandchild);

        Assert.That(root.IsExpanded, Is.True);
        Assert.That(child.IsExpanded, Is.True);
        Assert.That(grandchild.IsExpanded, Is.False);
    }

    [Test]
    public void ExpandToNode_RootNode_NoError()
    {
        var vm = new ProductTreeViewModel();
        var root = new TreeNodeModel { Text = "Root" };

        Assert.DoesNotThrow(() => vm.ExpandToNode(root));
        Assert.That(root.IsExpanded, Is.False);
    }

    [Test]
    public void ExpandToNode_DeeplyNested_ExpandsAll()
    {
        var vm = new ProductTreeViewModel();
        var nodes = new TreeNodeModel[5];
        for (var i = 0; i < 5; i++)
        {
            nodes[i] = new TreeNodeModel { Text = $"Level{i}" };
            if (i > 0) nodes[i].Parent = nodes[i - 1];
        }

        vm.ExpandToNode(nodes[4]);

        for (var i = 0; i < 4; i++)
            Assert.That(nodes[i].IsExpanded, Is.True, $"Level{i} should be expanded");
        Assert.That(nodes[4].IsExpanded, Is.False, "Target node itself should not be expanded");
    }

    [Test]
    public void ExpandToNode_AlreadyExpanded_NoError()
    {
        var vm = new ProductTreeViewModel();
        var root = new TreeNodeModel { Text = "Root", IsExpanded = true };
        var child = new TreeNodeModel { Text = "Child", Parent = root };

        Assert.DoesNotThrow(() => vm.ExpandToNode(child));
        Assert.That(root.IsExpanded, Is.True);
    }
}

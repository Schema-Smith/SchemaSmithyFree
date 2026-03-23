// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;

namespace SchemaHammer.UnitTests;

public class TreeNodeModelTests
{
    [Test]
    public void FullTreePath_ReturnsBackslashSeparatedAncestorChain()
    {
        var root = new TreeNodeModel { Text = "Root" };
        var child = new TreeNodeModel { Text = "Child", Parent = root };
        var grandchild = new TreeNodeModel { Text = "Grandchild", Parent = child };

        Assert.That(grandchild.FullTreePath, Is.EqualTo("Root\\Child\\Grandchild"));
    }

    [Test]
    public void FullTreePath_SingleNode_ReturnsNodeText()
    {
        var node = new TreeNodeModel { Text = "Root" };
        Assert.That(node.FullTreePath, Is.EqualTo("Root"));
    }

    [Test]
    public void LazyExpansion_FiresExpandActionOnFirstExpand()
    {
        var expanded = false;
        var node = new TreeNodeModel { Text = "Lazy" };
        node.Children.Add(new TreeNodeModel { Text = "placeholder" });
        node.ExpandAction = () => { expanded = true; };

        node.IsExpanded = true;

        Assert.That(expanded, Is.True);
    }

    [Test]
    public void LazyExpansion_DoesNotFireTwice()
    {
        var count = 0;
        var node = new TreeNodeModel { Text = "Lazy" };
        node.Children.Add(new TreeNodeModel { Text = "placeholder" });
        node.ExpandAction = () =>
        {
            count++;
            node.Children.Add(new TreeNodeModel { Text = "real" });
        };

        node.IsExpanded = true;
        node.IsExpanded = false;
        node.IsExpanded = true;

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void LazyExpansion_ClearsChildrenBeforeExpandAction()
    {
        var childrenAtExpand = -1;
        var node = new TreeNodeModel { Text = "Lazy" };
        node.Children.Add(new TreeNodeModel { Text = "placeholder" });
        node.ExpandAction = () => { childrenAtExpand = node.Children.Count; };

        node.IsExpanded = true;

        Assert.That(childrenAtExpand, Is.EqualTo(0));
    }

    [Test]
    public void EnsureExpanded_ExpandsIfNotExpanded()
    {
        var expanded = false;
        var node = new TreeNodeModel { Text = "Test" };
        node.ExpandAction = () => { expanded = true; };

        node.EnsureExpanded();

        Assert.That(node.IsExpanded, Is.True);
        Assert.That(expanded, Is.True);
    }

    [Test]
    public void GetAncestorChain_ReturnsRootToSelf()
    {
        var root = new TreeNodeModel { Text = "Root" };
        var child = new TreeNodeModel { Text = "Child", Parent = root };
        var grandchild = new TreeNodeModel { Text = "Grandchild", Parent = child };

        var chain = grandchild.GetAncestorChain();

        Assert.That(chain, Has.Count.EqualTo(3));
        Assert.That(chain[0].Text, Is.EqualTo("Root"));
        Assert.That(chain[1].Text, Is.EqualTo("Child"));
        Assert.That(chain[2].Text, Is.EqualTo("Grandchild"));
    }

    [Test]
    public void FindByTreePath_FindsNestedNode()
    {
        var root = new TreeNodeModel { Text = "Root" };
        var child = new TreeNodeModel { Text = "Child", Parent = root };
        root.Children.Add(child);
        var grandchild = new TreeNodeModel { Text = "Grandchild", Parent = child };
        child.Children.Add(grandchild);

        var found = root.FindByTreePath("Root\\Child\\Grandchild");

        Assert.That(found, Is.SameAs(grandchild));
    }

    [Test]
    public void FindByTreePath_ReturnsNullForMissingPath()
    {
        var root = new TreeNodeModel { Text = "Root" };
        root.Children.Add(new TreeNodeModel { Text = "Child", Parent = root });

        var found = root.FindByTreePath("Root\\Missing");

        Assert.That(found, Is.Null);
    }

    [Test]
    public void FindByTreePath_TriggersLazyExpansion()
    {
        var root = new TreeNodeModel { Text = "Root" };
        root.Children.Add(new TreeNodeModel { Text = "placeholder" });
        var realChild = new TreeNodeModel { Text = "Child", Parent = root };
        root.ExpandAction = () => { root.Children.Add(realChild); };

        var found = root.FindByTreePath("Root\\Child");

        Assert.That(found, Is.SameAs(realChild));
    }

    [Test]
    public void CollapseAllChildren_CollapsesRecursively()
    {
        var root = new TreeNodeModel { Text = "Root" };
        var child = new TreeNodeModel { Text = "Child", Parent = root, IsExpanded = true };
        root.Children.Add(child);
        var grandchild = new TreeNodeModel { Text = "Grandchild", Parent = child, IsExpanded = true };
        child.Children.Add(grandchild);

        root.CollapseAllChildren();

        Assert.That(child.IsExpanded, Is.False);
        Assert.That(grandchild.IsExpanded, Is.False);
    }
}

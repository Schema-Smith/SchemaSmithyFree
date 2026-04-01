// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.FunctionalTests.Fixtures;

public static class AssertionHelpers
{
    public static TreeNodeModel? FindNode(IReadOnlyList<TreeNodeModel> roots, string treePath)
    {
        var segments = treePath.Split('/');
        IReadOnlyList<TreeNodeModel> current = roots;

        TreeNodeModel? found = null;
        foreach (var segment in segments)
        {
            found = current.FirstOrDefault(n =>
                n.Text.Equals(segment, StringComparison.OrdinalIgnoreCase));

            if (found == null) return null;

            // Trigger lazy expansion if the node has an unexpanded placeholder
            if (found.ExpandAction != null)
                found.EnsureExpanded();

            current = found.Children;
        }

        return found;
    }

    public static TreeNodeModel AssertTreeContainsNode(IReadOnlyList<TreeNodeModel> roots, string treePath)
    {
        var node = FindNode(roots, treePath);
        Assert.That(node, Is.Not.Null, $"Expected node at path '{treePath}' but it was not found");
        return node!;
    }

    public static void AssertTreeDoesNotContainNode(IReadOnlyList<TreeNodeModel> roots, string treePath)
    {
        var node = FindNode(roots, treePath);
        Assert.That(node, Is.Null, $"Expected no node at path '{treePath}' but found one");
    }

    public static void AssertChildrenContain(TreeNodeModel parent, params string[] expectedNames)
    {
        var childNames = parent.Children.Select(c => c.Text).ToList();
        foreach (var expected in expectedNames)
        {
            Assert.That(childNames, Has.Some.EqualTo(expected).IgnoreCase,
                $"Expected child '{expected}' under '{parent.Text}' but found: [{string.Join(", ", childNames)}]");
        }
    }

    public static void AssertChildCount(TreeNodeModel parent, int expectedCount)
    {
        Assert.That(parent.Children.Count, Is.EqualTo(expectedCount),
            $"Expected {expectedCount} children under '{parent.Text}' but found {parent.Children.Count}");
    }

    public static TEditor AssertEditorType<TEditor>(IEditorService editorService, TreeNodeModel node)
        where TEditor : EditorBaseViewModel
    {
        var editor = editorService.GetEditor(node);
        Assert.That(editor, Is.Not.Null, $"EditorService returned null for node '{node.Text}'");
        Assert.That(editor, Is.InstanceOf<TEditor>(),
            $"Expected editor type {typeof(TEditor).Name} but got {editor!.GetType().Name}");
        return (TEditor)editor;
    }

    public static IEnumerable<TreeNodeModel> FindAllNodes(TreeNodeModel node)
    {
        // Trigger lazy expansion if needed
        if (node.ExpandAction != null)
            node.EnsureExpanded();

        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in FindAllNodes(child))
                yield return descendant;
    }
}

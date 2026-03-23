// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class IndexedViewEditorViewModelTests
{
    private static IndexedViewNodeModel MakeNode(IndexedView? iv = null)
    {
        var indexedView = iv ?? new IndexedView
        {
            Name = "vwTest",
            Schema = "dbo",
            Definition = "SELECT 1 AS Col1"
        };
        return new IndexedViewNodeModel
        {
            Text = $"{indexedView.Schema}.{indexedView.Name}",
            Tag = "Indexed View",
            IndexedViewData = indexedView
        };
    }

    [Test]
    public void ChangeNode_LoadsIndexedViewProperties()
    {
        var iv = new IndexedView { Name = "vwTest", Schema = "dbo", Definition = "SELECT 1" };
        var node = MakeNode(iv);

        var vm = new IndexedViewEditorViewModel();
        vm.ChangeNode(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Schema, Is.EqualTo("dbo"));
            Assert.That(vm.Name, Is.EqualTo("vwTest"));
            Assert.That(vm.Definition, Is.EqualTo("SELECT 1"));
        });
    }

    [Test]
    public void ChangeNode_PopulatesIndexSummary()
    {
        var iv = new IndexedView { Name = "vwTest", Schema = "dbo", Definition = "SELECT 1" };
        iv.Indexes.Add(new Schema.Domain.Index { Name = "[IX_vwTest]", IndexColumns = "Col1" });
        iv.Indexes.Add(new Schema.Domain.Index { Name = "[IX_vwTest_2]", IndexColumns = "Col2" });
        var node = MakeNode(iv);

        var vm = new IndexedViewEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.IndexSummary, Has.Count.EqualTo(2));
        Assert.That(vm.IndexSummary[0], Is.EqualTo("[IX_vwTest]"));
    }

    [Test]
    public void ChangeNode_IndexSummary_IsEmptyWhenNoIndexes()
    {
        var iv = new IndexedView { Name = "vwNoIndexes", Schema = "dbo", Definition = "SELECT 1" };
        var node = MakeNode(iv);

        var vm = new IndexedViewEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.IndexSummary, Is.Empty);
    }

    [Test]
    public void EditorTitle_ReturnsSchemaQualifiedName()
    {
        var iv = new IndexedView { Name = "vwTest", Schema = "reports" };
        var node = MakeNode(iv);

        var vm = new IndexedViewEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.EditorTitle, Is.EqualTo("reports.vwTest"));
    }

    [Test]
    public void ChangeNode_WithNullData_DoesNotThrow()
    {
        var node = new IndexedViewNodeModel
        {
            Text = "Empty",
            Tag = "Indexed View",
            IndexedViewData = null
        };
        var vm = new IndexedViewEditorViewModel();

        Assert.DoesNotThrow(() => vm.ChangeNode(node));
    }

    [Test]
    public void ChangeNode_WithNullData_LeavesPropertiesEmpty()
    {
        var node = new IndexedViewNodeModel
        {
            Text = "Empty",
            Tag = "Indexed View",
            IndexedViewData = null
        };
        var vm = new IndexedViewEditorViewModel();
        vm.ChangeNode(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Schema, Is.EqualTo(""));
            Assert.That(vm.Name, Is.EqualTo(""));
            Assert.That(vm.Definition, Is.EqualTo(""));
        });
    }

    [Test]
    public void ChangeNode_WithPlainTreeNodeModel_DoesNotThrow()
    {
        // Passing a plain TreeNodeModel (not IndexedViewNodeModel) should be handled gracefully
        var node = new TreeNodeModel { Text = "dbo.vwTest", Tag = "Indexed View" };
        var vm = new IndexedViewEditorViewModel();

        Assert.DoesNotThrow(() => vm.ChangeNode(node));
    }

    [Test]
    public void ChangeNode_ClearsIndexSummaryOnSubsequentCall()
    {
        var iv1 = new IndexedView { Name = "vwFirst", Schema = "dbo", Definition = "SELECT 1" };
        iv1.Indexes.Add(new Schema.Domain.Index { Name = "[IX_First]", IndexColumns = "Col1" });
        iv1.Indexes.Add(new Schema.Domain.Index { Name = "[IX_First_2]", IndexColumns = "Col2" });

        var iv2 = new IndexedView { Name = "vwSecond", Schema = "dbo", Definition = "SELECT 2" };
        // No indexes in second view

        var vm = new IndexedViewEditorViewModel();
        vm.ChangeNode(MakeNode(iv1));
        Assert.That(vm.IndexSummary, Has.Count.EqualTo(2));

        vm.ChangeNode(MakeNode(iv2));
        Assert.That(vm.IndexSummary, Is.Empty);
    }

    [Test]
    public void ChangeNode_SetsNodeReference()
    {
        var node = MakeNode();
        var vm = new IndexedViewEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.Node, Is.SameAs(node));
    }
}

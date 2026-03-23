// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class FullTextIndexEditorViewModelTests
{
    private static (TableNodeModel tableNode, TreeNodeModel ftiNode) SetupTableTree(Table table)
    {
        var tableNode = new TableNodeModel { Text = "dbo.T", Tag = "Table", TableData = table };
        var ftiNode = new TreeNodeModel { Text = "Full Text Index", Tag = "Full Text Index", Parent = tableNode };
        return (tableNode, ftiNode);
    }

    [Test]
    public void ChangeNode_LoadsFullTextIndexProperties()
    {
        var table = new Table { Name = "Articles", Schema = "dbo" };
        table.FullTextIndex = new FullTextIndex
        {
            FullTextCatalog = "[ArticleCatalog]",
            KeyIndex = "[PK_Articles]",
            ChangeTracking = "AUTO",
            StopList = "SYSTEM",
            Columns = "[Title]"
        };

        var (_, ftiNode) = SetupTableTree(table);

        var vm = new FullTextIndexEditorViewModel();
        vm.ChangeNode(ftiNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.FullTextCatalog, Is.EqualTo("ArticleCatalog"));
            Assert.That(vm.KeyIndex, Is.EqualTo("PK_Articles"));
            Assert.That(vm.ChangeTracking, Is.EqualTo("AUTO"));
            Assert.That(vm.StopList, Is.EqualTo("SYSTEM"));
            Assert.That(vm.Columns, Is.EqualTo("Title"));
        });
    }

    [Test]
    public void ChangeNode_StripsDisplayBrackets()
    {
        var table = new Table { Name = "Docs", Schema = "dbo" };
        table.FullTextIndex = new FullTextIndex
        {
            FullTextCatalog = "[DocCatalog]",
            KeyIndex = "[PK_Docs]",
            ChangeTracking = "MANUAL",
            StopList = "OFF",
            Columns = "[Content]"
        };

        var (_, ftiNode) = SetupTableTree(table);

        var vm = new FullTextIndexEditorViewModel();
        vm.ChangeNode(ftiNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.FullTextCatalog, Is.EqualTo("DocCatalog"));
            Assert.That(vm.KeyIndex, Is.EqualTo("PK_Docs"));
            Assert.That(vm.Columns, Is.EqualTo("Content"));
        });
    }

    [Test]
    public void ChangeNode_WithNoFullTextIndex_LeavesPropertiesEmpty()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        // FullTextIndex is null

        var (_, ftiNode) = SetupTableTree(table);

        var vm = new FullTextIndexEditorViewModel();
        vm.ChangeNode(ftiNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.FullTextCatalog, Is.EqualTo(""));
            Assert.That(vm.KeyIndex, Is.EqualTo(""));
            Assert.That(vm.ChangeTracking, Is.EqualTo(""));
            Assert.That(vm.StopList, Is.EqualTo(""));
            Assert.That(vm.Columns, Is.EqualTo(""));
        });
    }

    [Test]
    public void EditorTitle_ReturnsConstantString()
    {
        var vm = new FullTextIndexEditorViewModel();
        Assert.That(vm.EditorTitle, Is.EqualTo("Full Text Index"));
    }
}

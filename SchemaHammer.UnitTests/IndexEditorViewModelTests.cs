// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class IndexEditorViewModelTests
{
    [Test]
    public void ChangeNode_LoadsIndexFromTable_WithBracketedNames()
    {
        var table = new Table { Name = "Users", Schema = "dbo" };
        table.Indexes.Add(new Schema.Domain.Index { Name = "[PK_Users]", PrimaryKey = true, Clustered = true, IndexColumns = "Id" });

        var tableNode = new TableNodeModel { Text = "dbo.Users", Tag = "Table", TableData = table };
        var container = new TreeNodeModel { Text = "Indexes", Tag = "Index Container", Parent = tableNode };
        var indexNode = new TreeNodeModel { Text = "PK_Users", Tag = "Index", Parent = container };

        var vm = new IndexEditorViewModel();
        vm.ChangeNode(indexNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("PK_Users"));
            Assert.That(vm.PrimaryKey, Is.True);
            Assert.That(vm.Clustered, Is.True);
            Assert.That(vm.IndexColumns, Is.EqualTo("Id"));
        });
    }

    [Test]
    public void ChangeNode_LoadsIndexFromIndexedView()
    {
        var iv = new IndexedView { Name = "vwUsers", Schema = "dbo", Definition = "SELECT Id FROM dbo.Users" };
        iv.Indexes.Add(new Schema.Domain.Index { Name = "[IX_vwUsers]", Unique = true, Clustered = true, IndexColumns = "Id" });

        var ivNode = new IndexedViewNodeModel { Text = "dbo.vwUsers", Tag = "Indexed View", IndexedViewData = iv };
        var container = new TreeNodeModel { Text = "Indexes", Tag = "Index Container", Parent = ivNode };
        var indexNode = new TreeNodeModel { Text = "IX_vwUsers", Tag = "Index", Parent = container };

        var vm = new IndexEditorViewModel();
        vm.ChangeNode(indexNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("IX_vwUsers"));
            Assert.That(vm.Unique, Is.True);
            Assert.That(vm.Clustered, Is.True);
        });
    }

    [Test]
    public void EditorTitle_ReturnsStrippedName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.Indexes.Add(new Schema.Domain.Index { Name = "[IX_T_Col]", IndexColumns = "Col" });

        var tableNode = new TableNodeModel { Text = "dbo.T", Tag = "Table", TableData = table };
        var container = new TreeNodeModel { Text = "Indexes", Tag = "Index Container", Parent = tableNode };
        var indexNode = new TreeNodeModel { Text = "IX_T_Col", Tag = "Index", Parent = container };

        var vm = new IndexEditorViewModel();
        vm.ChangeNode(indexNode);
        Assert.That(vm.EditorTitle, Is.EqualTo("IX_T_Col"));
    }
}

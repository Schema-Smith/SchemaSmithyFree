// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class TableEditorViewModelTests
{
    [Test]
    public void ChangeNode_LoadsTableProperties()
    {
        var table = new Table { Name = "Users", Schema = "dbo", CompressionType = "PAGE", IsTemporal = true };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var node = new TableNodeModel { Text = "dbo.Users", Tag = "Table", TableData = table };
        var vm = new TableEditorViewModel();
        vm.ChangeNode(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.CompressionType, Is.EqualTo("PAGE"));
            Assert.That(vm.IsTemporal, Is.True);
        });
    }

    [Test]
    public void EditorTitle_ReturnsNodeText()
    {
        var table = new Table { Name = "Users", Schema = "dbo" };
        table.Columns.Add(new Column { Name = "Id", DataType = "int" });

        var node = new TableNodeModel { Text = "dbo.Users", Tag = "Table", TableData = table };
        var vm = new TableEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.EditorTitle, Is.EqualTo("dbo.Users"));
    }
}

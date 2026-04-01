// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class CheckConstraintEditorViewModelTests
{
    private static (TableNodeModel tableNode, TreeNodeModel componentNode) SetupTableTree(
        Table table, string componentText)
    {
        var tableNode = new TableNodeModel { Text = "dbo.T", Tag = "Table", TableData = table };
        var container = new TreeNodeModel { Text = "Check Constraints", Tag = "Check Constraint Container", Parent = tableNode };
        var componentNode = new TreeNodeModel { Text = componentText, Tag = "Check Constraint", Parent = container };
        return (tableNode, componentNode);
    }

    [Test]
    public void ChangeNode_LoadsCheckConstraintProperties()
    {
        var table = new Table { Name = "Orders", Schema = "dbo" };
        table.CheckConstraints.Add(new CheckConstraint
        {
            Name = "[CK_Orders_Amount]",
            Expression = "[Amount] > 0"
        });

        var (_, ccNode) = SetupTableTree(table, "CK_Orders_Amount");

        var vm = new CheckConstraintEditorViewModel();
        vm.ChangeNode(ccNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("CK_Orders_Amount"));
            Assert.That(vm.Expression, Is.EqualTo("[Amount] > 0"));
        });
    }

    [Test]
    public void ChangeNode_StripsDisplayName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.CheckConstraints.Add(new CheckConstraint
        {
            Name = "[CK_T_Status]",
            Expression = "[Status] IN ('A','B')"
        });

        var (_, ccNode) = SetupTableTree(table, "CK_T_Status");

        var vm = new CheckConstraintEditorViewModel();
        vm.ChangeNode(ccNode);

        Assert.That(vm.Name, Is.EqualTo("CK_T_Status"));
    }

    [Test]
    public void EditorTitle_ReturnsStrippedName()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        table.CheckConstraints.Add(new CheckConstraint
        {
            Name = "[CK_T_Val]",
            Expression = "[Val] >= 0"
        });

        var (_, ccNode) = SetupTableTree(table, "CK_T_Val");

        var vm = new CheckConstraintEditorViewModel();
        vm.ChangeNode(ccNode);

        Assert.That(vm.EditorTitle, Is.EqualTo("CK_T_Val"));
    }

    [Test]
    public void ChangeNode_WithNoMatchingConstraint_LeavesPropertiesEmpty()
    {
        var table = new Table { Name = "T", Schema = "dbo" };
        // No check constraints added

        var (_, ccNode) = SetupTableTree(table, "CK_T_Missing");

        var vm = new CheckConstraintEditorViewModel();
        vm.ChangeNode(ccNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo(""));
            Assert.That(vm.Expression, Is.EqualTo(""));
        });
    }
}

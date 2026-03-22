using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class ContainerEditorViewModelTests
{
    [Test]
    public void ContainerName_ReturnsNodeText()
    {
        var vm = new ContainerEditorViewModel();
        var node = new TreeNodeModel { Text = "Columns", Tag = "Column Container" };
        vm.ChangeNode(node);
        Assert.That(vm.ContainerName, Is.EqualTo("Columns"));
    }

    [Test]
    public void ParentContext_BuildsPathFromAncestors()
    {
        var root = new TreeNodeModel { Text = "Templates" };
        var template = new TreeNodeModel { Text = "Main", Parent = root };
        var table = new TreeNodeModel { Text = "dbo.Users", Parent = template };
        var container = new TreeNodeModel { Text = "Columns", Tag = "Column Container", Parent = table };

        var vm = new ContainerEditorViewModel();
        vm.ChangeNode(container);

        Assert.That(vm.ParentContext, Is.EqualTo("Templates / Main / dbo.Users"));
    }

    [Test]
    public void ParentContext_EmptyWhenNoParent()
    {
        var vm = new ContainerEditorViewModel();
        vm.ChangeNode(new TreeNodeModel { Text = "Root" });
        Assert.That(vm.ParentContext, Is.EqualTo(""));
    }
}

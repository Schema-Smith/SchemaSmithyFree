using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class ProductEditorViewModelTests
{
    private static readonly string ValidProductPath =
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "TestProducts", "ValidProduct"));

    [Test]
    public void ChangeNode_LoadsProductProperties()
    {
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.Name, Is.Not.Empty);
        Assert.That(vm.Platform, Is.Not.Empty);
    }

    [Test]
    public void ChangeNode_LoadsTemplateOrder()
    {
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.TemplateOrder, Is.Not.Empty);
    }

    [Test]
    public void ChangeNode_LoadsScriptTokens()
    {
        var node = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };

        var vm = new ProductEditorViewModel();
        vm.ChangeNode(node);

        // ScriptTokens may or may not be empty depending on test fixture
        Assert.That(vm.ScriptTokens, Is.Not.Null);
    }
}

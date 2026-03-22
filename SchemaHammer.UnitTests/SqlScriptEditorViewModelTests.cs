using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class SqlScriptEditorViewModelTests
{
    private static readonly string ValidProductPath =
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "TestProducts", "ValidProduct"));

    [Test]
    public void ChangeNode_LoadsScriptContent()
    {
        var scriptPath = Path.Combine(ValidProductPath,
            "Templates", "Main", "Functions", "dbo.MyFunction.sql");
        var node = new TreeNodeModel
        {
            Text = "dbo.MyFunction.sql",
            Tag = "Sql Script",
            NodePath = scriptPath
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.DisplayContent, Is.Not.Empty);
    }

    [Test]
    public void ExpandTokens_ReplacesTripleBraceTokens()
    {
        var vm = new SqlScriptEditorViewModel();
        // Without tree context, no tokens found, so string unchanged
        var result = vm.ExpandTokens("SELECT {{{MyToken}}} FROM dbo.T");
        Assert.That(result, Is.EqualTo("SELECT {{{MyToken}}} FROM dbo.T"));
    }

    [Test]
    public void TogglePreview_TogglesIsPreviewMode()
    {
        var vm = new SqlScriptEditorViewModel();
        Assert.That(vm.IsPreviewMode, Is.False);

        vm.TogglePreviewCommand.Execute(null);
        Assert.That(vm.IsPreviewMode, Is.True);

        vm.TogglePreviewCommand.Execute(null);
        Assert.That(vm.IsPreviewMode, Is.False);
    }

    [Test]
    public void ChangeNode_ResetsPreviewMode()
    {
        var scriptPath = Path.Combine(ValidProductPath,
            "Templates", "Main", "Functions", "dbo.MyFunction.sql");
        var node = new TreeNodeModel
        {
            Text = "dbo.MyFunction.sql",
            Tag = "Sql Script",
            NodePath = scriptPath
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(node);
        vm.TogglePreviewCommand.Execute(null);
        Assert.That(vm.IsPreviewMode, Is.True);

        // Change to different node resets preview
        vm.ChangeNode(node);
        Assert.That(vm.IsPreviewMode, Is.False);
    }
}

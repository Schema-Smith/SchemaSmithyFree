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

    [Test]
    public void CollectScriptTokens_WithProductAndTemplateNodes_MergesTokens()
    {
        // CollectScriptTokens walks up the Parent chain from the script node.
        // productNode.NodePath = ValidProductPath (directory containing Product.json)
        // templateNode.NodePath = Templates/Main directory (CollectScriptTokens appends "Template.json")
        var templateDir = Path.Combine(ValidProductPath, "Templates", "Main");
        var scriptPath = Path.Combine(templateDir, "Functions", "dbo.MyFunction.sql");

        var productNode = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };
        var templatesContainer = new TreeNodeModel
        {
            Text = "Templates",
            Tag = "Templates",
            Parent = productNode
        };
        var templateNode = new TreeNodeModel
        {
            Text = "Main",
            Tag = "Template",
            NodePath = templateDir,
            Parent = templatesContainer
        };
        var functionsContainer = new TreeNodeModel
        {
            Text = "Functions",
            Tag = "Functions Container",
            Parent = templateNode
        };
        var scriptNode = new TreeNodeModel
        {
            Text = "dbo.MyFunction.sql",
            Tag = "Sql Script",
            NodePath = scriptPath,
            Parent = functionsContainer
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(scriptNode);

        var tokens = vm.CollectScriptTokens();

        // ValidProduct/Product.json has: SecondaryDB, MainDB, ReleaseVersion
        Assert.That(tokens, Is.Not.Empty);
        Assert.That(tokens.ContainsKey("MainDB"), Is.True);
        Assert.That(tokens["MainDB"], Is.EqualTo("TestMain"));
    }

    [Test]
    public void CollectScriptTokens_WithNoParentNodes_ReturnsEmptyDictionary()
    {
        var node = new TreeNodeModel
        {
            Text = "orphan.sql",
            Tag = "Sql Script",
            NodePath = ""
        };
        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(node);

        var tokens = vm.CollectScriptTokens();

        Assert.That(tokens, Is.Empty);
    }

    [Test]
    public void ExpandTokens_WithRealTokens_ReplacesTripleBraces()
    {
        var templateDir = Path.Combine(ValidProductPath, "Templates", "Main");
        var scriptPath = Path.Combine(templateDir, "Functions", "dbo.MyFunction.sql");

        var productNode = new TreeNodeModel
        {
            Text = "ValidProduct",
            Tag = "Product",
            NodePath = ValidProductPath
        };
        var templateNode = new TreeNodeModel
        {
            Text = "Main",
            Tag = "Template",
            NodePath = templateDir,
            Parent = productNode
        };
        var scriptNode = new TreeNodeModel
        {
            Text = "dbo.MyFunction.sql",
            Tag = "Sql Script",
            NodePath = scriptPath,
            Parent = templateNode
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(scriptNode);

        var result = vm.ExpandTokens("USE {{{MainDB}}}");

        Assert.That(result, Is.EqualTo("USE TestMain"));
    }

    [Test]
    public void EditorTitle_ReturnsNodeText()
    {
        var node = new TreeNodeModel { Text = "deploy.sql", Tag = "Sql Script" };
        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(node);

        Assert.That(vm.EditorTitle, Is.EqualTo("deploy.sql"));
    }

    [Test]
    public void EditorTitle_BeforeChangeNode_ReturnsFallback()
    {
        var vm = new SqlScriptEditorViewModel();
        Assert.That(vm.EditorTitle, Is.EqualTo("Script"));
    }
}

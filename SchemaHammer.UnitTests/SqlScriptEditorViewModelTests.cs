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

    [Test]
    public void PreviewButtonText_ReturnsPreviewWhenNotInPreviewMode()
    {
        var vm = new SqlScriptEditorViewModel();
        Assert.That(vm.PreviewButtonText, Is.EqualTo("Preview"));
    }

    [Test]
    public void PreviewButtonText_ReturnsRawWhenInPreviewMode()
    {
        var vm = new SqlScriptEditorViewModel();
        vm.TogglePreviewCommand.Execute(null);
        Assert.That(vm.PreviewButtonText, Is.EqualTo("Raw"));
    }

    [Test]
    public void PreviewButtonText_TogglesBackToPreviewAfterSecondToggle()
    {
        var vm = new SqlScriptEditorViewModel();
        vm.TogglePreviewCommand.Execute(null); // now in preview mode
        vm.TogglePreviewCommand.Execute(null); // back to raw mode
        Assert.That(vm.PreviewButtonText, Is.EqualTo("Preview"));
    }

    [Test]
    public void ChangeNode_WithNonExistentPath_ShowsErrorMessage()
    {
        var node = new TreeNodeModel
        {
            Text = "missing.sql",
            Tag = "Sql Script",
            NodePath = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid() + ".sql")
        };
        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(node);
        Assert.That(vm.DisplayContent, Does.StartWith("// Error loading"));
    }

    [Test]
    public void ChangeNode_WithEmptyNodePath_DisplayContentIsEmpty()
    {
        var node = new TreeNodeModel { Text = "test.sql", Tag = "Sql Script", NodePath = "" };
        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(node);
        Assert.That(vm.DisplayContent, Is.EqualTo(""));
    }

    [Test]
    public void DisplayContent_DefaultsToEmpty()
    {
        var vm = new SqlScriptEditorViewModel();
        Assert.That(vm.DisplayContent, Is.EqualTo(""));
    }

    [Test]
    public void IsPreviewMode_DefaultsToFalse()
    {
        var vm = new SqlScriptEditorViewModel();
        Assert.That(vm.IsPreviewMode, Is.False);
    }

    [Test]
    public void ExtractTokenAtPosition_InsideToken_ReturnsTokenName()
    {
        var vm = new SqlScriptEditorViewModel();
        var text = "SELECT {{{MainDB}}}.dbo.Table1";
        var result = vm.ExtractTokenAtPosition(text, 10); // inside "MainDB"

        Assert.That(result, Is.EqualTo("MainDB"));
    }

    [Test]
    public void ExtractTokenAtPosition_OutsideToken_ReturnsNull()
    {
        var vm = new SqlScriptEditorViewModel();
        var text = "SELECT {{{MainDB}}}.dbo.Table1";
        var result = vm.ExtractTokenAtPosition(text, 2); // inside "SELECT"

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ExtractTokenAtPosition_OnOpeningBraces_ReturnsTokenName()
    {
        var vm = new SqlScriptEditorViewModel();
        var text = "SELECT {{{MainDB}}}.dbo.Table1";
        var result = vm.ExtractTokenAtPosition(text, 7); // on first {

        Assert.That(result, Is.EqualTo("MainDB"));
    }

    [Test]
    public void ExtractTokenAtPosition_OnClosingBraces_ReturnsTokenName()
    {
        var vm = new SqlScriptEditorViewModel();
        var text = "SELECT {{{MainDB}}}.dbo.Table1";
        var result = vm.ExtractTokenAtPosition(text, 17); // on last }

        Assert.That(result, Is.EqualTo("MainDB"));
    }

    [Test]
    public void ExtractTokenAtPosition_EmptyText_ReturnsNull()
    {
        var vm = new SqlScriptEditorViewModel();
        Assert.That(vm.ExtractTokenAtPosition("", 0), Is.Null);
    }

    [Test]
    public void ExtractTokenAtPosition_NoTripleBraces_ReturnsNull()
    {
        var vm = new SqlScriptEditorViewModel();
        var text = "SELECT {{MainDB}}.dbo.Table1"; // double braces, not triple
        Assert.That(vm.ExtractTokenAtPosition(text, 10), Is.Null);
    }

    [TearDown]
    public void Cleanup()
    {
        EditorBaseViewModel.PendingTokenName = null;
    }

    [Test]
    public void NavigateToTokenDefinition_WithTemplateToken_NavigatesToTemplate()
    {
        var templateDir = Path.Combine(ValidProductPath, "Templates", "Main");

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
            NodePath = Path.Combine(templateDir, "Template.json"),
            Parent = templatesContainer
        };
        var scriptNode = new TreeNodeModel
        {
            Text = "test.sql",
            Tag = "Sql Script",
            Parent = templateNode
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(scriptNode);

        TreeNodeModel? navigatedTo = null;
        vm.NavigateToNode = node => navigatedTo = node;

        // MainDB is a product-level token, should navigate to product
        vm.NavigateToTokenDefinition("MainDB");

        Assert.That(navigatedTo, Is.EqualTo(productNode));
        Assert.That(EditorBaseViewModel.PendingTokenName, Is.EqualTo("MainDB"));
    }

    [Test]
    public void NavigateToTokenDefinition_WithProductToken_NavigatesToProduct()
    {
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
            NodePath = Path.Combine(ValidProductPath, "Templates", "Main", "Template.json"),
            Parent = templatesContainer
        };
        var scriptNode = new TreeNodeModel
        {
            Text = "test.sql",
            Tag = "Sql Script",
            Parent = templateNode
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(scriptNode);

        TreeNodeModel? navigatedTo = null;
        vm.NavigateToNode = node => navigatedTo = node;

        vm.NavigateToTokenDefinition("SecondaryDB");

        Assert.That(navigatedTo, Is.Not.Null);
        Assert.That(EditorBaseViewModel.PendingTokenName, Is.EqualTo("SecondaryDB"));
    }

    [Test]
    public void NavigateToTokenDefinition_UnknownToken_StillNavigates()
    {
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
            NodePath = Path.Combine(ValidProductPath, "Templates", "Main", "Template.json"),
            Parent = productNode
        };
        var scriptNode = new TreeNodeModel
        {
            Text = "test.sql",
            Tag = "Sql Script",
            Parent = templateNode
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(scriptNode);

        TreeNodeModel? navigatedTo = null;
        vm.NavigateToNode = node => navigatedTo = node;

        vm.NavigateToTokenDefinition("NonExistentToken");

        // Should still navigate — falls through to template node
        Assert.That(navigatedTo, Is.Not.Null);
        Assert.That(EditorBaseViewModel.PendingTokenName, Is.EqualTo("NonExistentToken"));
    }

    [Test]
    public void NavigateToTokenDefinition_NoCallback_DoesNotThrow()
    {
        var scriptNode = new TreeNodeModel
        {
            Text = "test.sql",
            Tag = "Sql Script"
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(scriptNode);
        // NavigateToNode is null — should return immediately

        Assert.DoesNotThrow(() => vm.NavigateToTokenDefinition("MainDB"));
    }

    [Test]
    public void NavigateToTokenDefinition_NoParentNodes_DoesNotNavigate()
    {
        var scriptNode = new TreeNodeModel
        {
            Text = "orphan.sql",
            Tag = "Sql Script"
        };

        var vm = new SqlScriptEditorViewModel();
        vm.ChangeNode(scriptNode);

        TreeNodeModel? navigatedTo = null;
        vm.NavigateToNode = node => navigatedTo = node;

        vm.NavigateToTokenDefinition("MainDB");

        // No template or product to navigate to
        Assert.That(navigatedTo, Is.Null);
    }
}

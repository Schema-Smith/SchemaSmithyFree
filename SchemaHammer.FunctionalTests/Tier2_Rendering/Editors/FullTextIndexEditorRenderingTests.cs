// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: FullTextIndexEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class FullTextIndexEditorRenderingTests : TestProductFixture
{
    [AvaloniaTest]
    public void FullTextIndexEditorView_RendersAndBindings()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Test]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithColumn("[Description]", "nvarchar(max)")
                    .WithFullTextIndex("[Description]", "[FT_Catalog]", "[PK_Test]")))
            .Build(TempDir);

        var treeService = new ProductTreeService();
        var roots = treeService.LoadProduct(TempDir);
        var editorService = new EditorService();

        var node = roots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Contains("Full Text Index", StringComparison.OrdinalIgnoreCase));
        Assert.That(node, Is.Not.Null, "Full Text Index node not found in tree");

        var vm = AssertionHelpers.AssertEditorType<FullTextIndexEditorViewModel>(editorService, node!);
        var view = new FullTextIndexEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(view.IsVisible, Is.True);
        Assert.That(vm.EditorTitle, Is.EqualTo("Full Text Index"));
        Assert.That(vm.FullTextCatalog, Is.Not.Null.And.Not.Empty);

        window.Close();
    }
}

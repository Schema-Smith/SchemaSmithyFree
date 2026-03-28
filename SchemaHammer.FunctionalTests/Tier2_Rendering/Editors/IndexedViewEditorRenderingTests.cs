// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: IndexedViewEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class IndexedViewEditorRenderingTests : TestProductFixture
{
    [AvaloniaTest]
    public void IndexedViewEditorView_RendersAndBindings()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Test]", table => table
                    .WithColumn("[Id]", "int", nullable: false))
                .WithIndexedView("vw_TestIndexed", "dbo",
                    "SELECT [Id] FROM [dbo].[Test] WITH (NOEXPAND)"))
            .Build(TempDir);

        var treeService = new ProductTreeService();
        var roots = treeService.LoadProduct(TempDir);
        var editorService = new EditorService();

        var node = roots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Contains("vw_TestIndexed", StringComparison.OrdinalIgnoreCase));
        Assert.That(node, Is.Not.Null, "vw_TestIndexed node not found in tree");

        var vm = AssertionHelpers.AssertEditorType<IndexedViewEditorViewModel>(editorService, node!);
        var view = new IndexedViewEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(view.IsVisible, Is.True);
        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Name, Is.EqualTo("vw_TestIndexed"));
        Assert.That(vm.Definition, Is.Not.Null.And.Not.Empty);

        window.Close();
    }
}

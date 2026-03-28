// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: StatisticEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class StatisticEditorRenderingTests : TestProductFixture
{
    [AvaloniaTest]
    public void StatisticEditorView_RendersAndBindings()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Test]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithColumn("[Name]", "nvarchar(100)")
                    .WithStatistic("[ST_Test_Name]", "[Name]")))
            .Build(TempDir);

        var treeService = new ProductTreeService();
        var roots = treeService.LoadProduct(TempDir);
        var editorService = new EditorService();

        var node = roots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Contains("ST_Test_Name", StringComparison.OrdinalIgnoreCase));
        Assert.That(node, Is.Not.Null, "ST_Test_Name node not found in tree");

        var vm = AssertionHelpers.AssertEditorType<StatisticEditorViewModel>(editorService, node!);
        var view = new StatisticEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(view.IsVisible, Is.True);
        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Name, Is.EqualTo("ST_Test_Name"));

        window.Close();
    }
}

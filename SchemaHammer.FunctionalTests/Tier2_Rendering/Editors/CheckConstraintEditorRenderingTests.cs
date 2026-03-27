// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: CheckConstraintEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class CheckConstraintEditorRenderingTests : TestProductFixture
{
    [AvaloniaTest]
    public void CheckConstraintEditorView_RendersAndBindings()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Test]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithCheckConstraint("[CK_Test_Id]", "[Id] > 0")))
            .Build(TempDir);

        var treeService = new ProductTreeService();
        var roots = treeService.LoadProduct(TempDir);
        var editorService = new EditorService();

        var node = roots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Contains("CK_Test_Id", StringComparison.OrdinalIgnoreCase));
        Assert.That(node, Is.Not.Null, "CK_Test_Id node not found in tree");

        var vm = AssertionHelpers.AssertEditorType<CheckConstraintEditorViewModel>(editorService, node!);
        var view = new CheckConstraintEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(view.IsVisible, Is.True);
        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Expression, Is.EqualTo("[Id] > 0"));

        window.Close();
    }
}

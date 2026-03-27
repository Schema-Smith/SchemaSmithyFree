// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: XmlIndexEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class XmlIndexEditorRenderingTests : TestProductFixture
{
    [AvaloniaTest]
    public void XmlIndexEditorView_RendersAndBindings()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Test]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithColumn("[XmlData]", "xml")
                    .WithXmlIndex("[PXML_Test_XmlData]", "[XmlData]", isPrimary: true)))
            .Build(TempDir);

        var treeService = new ProductTreeService();
        var roots = treeService.LoadProduct(TempDir);
        var editorService = new EditorService();

        var node = roots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Contains("PXML_Test_XmlData", StringComparison.OrdinalIgnoreCase));
        Assert.That(node, Is.Not.Null, "PXML_Test_XmlData node not found in tree");

        var vm = AssertionHelpers.AssertEditorType<XmlIndexEditorViewModel>(editorService, node!);
        var view = new XmlIndexEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(view.IsVisible, Is.True);
        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Name, Is.EqualTo("PXML_Test_XmlData"));
        Assert.That(vm.IsPrimary, Is.True);

        window.Close();
    }
}

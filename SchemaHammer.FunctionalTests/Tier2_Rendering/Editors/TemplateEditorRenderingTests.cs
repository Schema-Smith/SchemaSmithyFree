// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: TemplateEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class TemplateEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void TemplateEditorView_RendersWithoutError()
    {
        var vm = GetTemplateEditor();
        var view = new TemplateEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void TemplateEditorView_BindingsReflectViewModel()
    {
        var vm = GetTemplateEditor();
        var view = new TemplateEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Name, Is.EqualTo("Main"));
        Assert.That(vm.DatabaseIdentificationScript, Is.Not.Null);

        window.Close();
    }
}

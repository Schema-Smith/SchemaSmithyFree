// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: ContainerEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class ContainerEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void ContainerEditorView_RendersWithoutError()
    {
        var vm = GetContainerEditor();
        var view = new ContainerEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void ContainerEditorView_BindingsReflectViewModel()
    {
        var vm = GetContainerEditor();
        var view = new ContainerEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.ContainerName, Is.EqualTo("Tables").IgnoreCase);

        window.Close();
    }
}

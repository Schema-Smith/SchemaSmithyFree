// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: IndexEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class IndexEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void IndexEditorView_RendersWithoutError()
    {
        var vm = GetIndexEditor();
        var view = new IndexEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void IndexEditorView_BindingsReflectViewModel()
    {
        var vm = GetIndexEditor();
        var view = new IndexEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Name, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.Unique, Is.True, "The IX_Users_Email index was created with unique: true");

        window.Close();
    }
}

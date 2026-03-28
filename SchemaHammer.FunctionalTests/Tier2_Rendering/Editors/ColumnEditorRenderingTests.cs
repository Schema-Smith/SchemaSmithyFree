// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: ColumnEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class ColumnEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void ColumnEditorView_RendersWithoutError()
    {
        var vm = GetColumnEditor();
        var view = new ColumnEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void ColumnEditorView_BindingsReflectViewModel()
    {
        var vm = GetColumnEditor();
        var view = new ColumnEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(vm.Name, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.DataType, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.OldName, Is.Not.Null);
        Assert.That(vm.Collation, Is.Not.Null);

        window.Close();
    }
}

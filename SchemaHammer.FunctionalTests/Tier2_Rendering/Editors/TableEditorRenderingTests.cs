// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: TableEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class TableEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void TableEditorView_RendersWithoutError()
    {
        var vm = GetTableEditor();
        var view = new TableEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void TableEditorView_BindingsReflectViewModel()
    {
        var vm = GetTableEditor();
        var view = new TableEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.CompressionType, Is.Not.Null);
        Assert.That(vm.OldName, Is.Not.Null);

        window.Close();
    }
}

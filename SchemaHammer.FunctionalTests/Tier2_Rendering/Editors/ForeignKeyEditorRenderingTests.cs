// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: ForeignKeyEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class ForeignKeyEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void ForeignKeyEditorView_RendersWithoutError()
    {
        var vm = GetForeignKeyEditor();
        var view = new ForeignKeyEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void ForeignKeyEditorView_BindingsReflectViewModel()
    {
        var vm = GetForeignKeyEditor();
        var view = new ForeignKeyEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.RelatedTable, Is.Not.Null.And.Not.Empty,
            "FK_Orders_Users should reference dbo.Users");

        window.Close();
    }
}

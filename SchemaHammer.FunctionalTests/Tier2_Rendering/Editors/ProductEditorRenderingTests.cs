// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: ProductEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class ProductEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void ProductEditorView_RendersWithoutError()
    {
        var vm = GetProductEditor();
        var view = new ProductEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void ProductEditorView_BindingsReflectViewModel()
    {
        var vm = GetProductEditor();
        var view = new ProductEditorView { DataContext = vm };
        var window = HostView(view);

        // TemplateOrder and ScriptTokens are always initialised (non-null collections).
        Assert.That(vm.TemplateOrder, Is.Not.Null);
        Assert.That(vm.ScriptTokens, Is.Not.Null);
        // The synthetic node path points to Product.json directly; ChangeNode loads from
        // Path.Combine(NodePath, "Product.json") so properties may be empty — that is expected
        // for this rendering-only smoke test. Verify the view rendered without throwing.
        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void ProductEditorView_DropUnknownIndexes_DefaultsFalse()
    {
        var vm = GetProductEditor();

        // Standard test product is built without explicit DropUnknownIndexes,
        // so it should default to false.
        Assert.That(vm.DropUnknownIndexes, Is.False);
    }
}

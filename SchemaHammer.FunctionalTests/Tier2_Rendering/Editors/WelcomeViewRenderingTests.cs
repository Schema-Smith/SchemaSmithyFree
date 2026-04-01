// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: WelcomeViewRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.ViewModels.Editors;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class WelcomeViewRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void WelcomeView_RendersWithoutError()
    {
        var vm = new WelcomeViewModel();
        var view = new WelcomeView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);
        Assert.That(vm.EditorTitle, Is.EqualTo("Welcome"));
        Assert.That(vm.WelcomeMessage, Is.Not.Null.And.Not.Empty);

        window.Close();
    }
}

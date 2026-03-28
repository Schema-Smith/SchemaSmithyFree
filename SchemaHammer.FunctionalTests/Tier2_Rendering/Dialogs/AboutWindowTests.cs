// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise exclusions: none — AboutWindow is Community-only (no Enterprise equivalent).

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using SchemaHammer.Views;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Dialogs;

[TestFixture]
public class AboutWindowTests : DialogTestBase
{
    [AvaloniaTest]
    public void AboutWindow_RendersWithoutError()
    {
        var dialog = new AboutWindow();
        HostDialog(dialog);

        Assert.That(dialog.IsVisible, Is.True);

        dialog.Close();
    }

    [AvaloniaTest]
    public void AboutWindow_GitHubLink_Exists()
    {
        var dialog = new AboutWindow();
        HostDialog(dialog);

        var link = FindControl<TextBlock>(dialog, "GitHubLink");
        Assert.That(link, Is.Not.Null, "GitHubLink TextBlock not found");

        dialog.Close();
    }
}

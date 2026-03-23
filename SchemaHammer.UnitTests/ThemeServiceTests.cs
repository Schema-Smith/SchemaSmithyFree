// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Services;
using SchemaHammer.Themes;

namespace SchemaHammer.UnitTests;

public class ThemeServiceTests
{
    [Test]
    public void GetAvailableThemes_ReturnsTwoBuiltInThemes()
    {
        var service = new ThemeService();
        var themes = service.GetAvailableThemes();
        Assert.That(themes, Has.Count.EqualTo(2));
        Assert.That(themes[0].Name, Is.EqualTo("Light"));
        Assert.That(themes[1].Name, Is.EqualTo("Dark"));
    }

    [Test]
    public void LoadTheme_ReturnsLight_ByDefault()
    {
        var service = new ThemeService();
        var theme = service.LoadTheme("Unknown");
        Assert.That(theme.Name, Is.EqualTo("Light"));
    }

    [Test]
    public void LoadTheme_ReturnsDark_WhenRequested()
    {
        var service = new ThemeService();
        var theme = service.LoadTheme("Dark");
        Assert.That(theme.Name, Is.EqualTo("Dark"));
        Assert.That(theme.BasedOn, Is.EqualTo("Dark"));
    }

    [Test]
    public void SetActive_UpdatesCurrent()
    {
        var service = new ThemeService();
        var dark = service.LoadTheme("Dark");
        service.SetActive(dark);
        Assert.That(service.Current.Name, Is.EqualTo("Dark"));
    }

    [Test]
    public void Current_DefaultsToLight()
    {
        var service = new ThemeService();
        Assert.That(service.Current.Name, Is.EqualTo("Light"));
    }
}

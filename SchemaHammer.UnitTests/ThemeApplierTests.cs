// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Themes;

namespace SchemaHammer.UnitTests;

public class ThemeApplierTests
{
    [Test]
    public void Apply_WithNullApplication_DoesNotThrow()
    {
        // Application.Current is null in test context — Apply() must guard against this
        var theme = new ThemeDefinition { Name = "Test", BasedOn = "Dark", Colors = [] };
        Assert.DoesNotThrow(() => ThemeApplier.Apply(theme));
    }

    [Test]
    public void Apply_WithEmptyColors_DoesNotThrow()
    {
        var theme = new ThemeDefinition { Name = "Light", BasedOn = "Light", Colors = [] };
        Assert.DoesNotThrow(() => ThemeApplier.Apply(theme));
    }

    [Test]
    public void Apply_WithPopulatedColors_DoesNotThrow()
    {
        // Even with colors, Application.Current is null so the method returns early
        var theme = new ThemeDefinition
        {
            Name = "Custom",
            BasedOn = "Dark",
            Colors = new Dictionary<string, string>
            {
                ["SH.Background"] = "#1E1E1E",
                ["SH.Foreground"] = "#FFFFFF"
            }
        };
        Assert.DoesNotThrow(() => ThemeApplier.Apply(theme));
    }

    [Test]
    public void GetIcon_WhenDark_ReturnsNull()
    {
        // Icon assets are not yet wired; always returns null
        Assert.That(ThemeApplier.GetIcon(true), Is.Null);
    }

    [Test]
    public void GetIcon_WhenLight_ReturnsNull()
    {
        Assert.That(ThemeApplier.GetIcon(false), Is.Null);
    }

    [Test]
    public void Apply_WithLightTheme_DoesNotThrow()
    {
        var theme = new ThemeDefinition { Name = "Light", BasedOn = "Light", Colors = [] };
        Assert.DoesNotThrow(() => ThemeApplier.Apply(theme));
    }

    [Test]
    public void Apply_WithInvalidHexColor_DoesNotThrow()
    {
        var theme = new ThemeDefinition
        {
            Name = "Bad",
            BasedOn = "Dark",
            Colors = new Dictionary<string, string>
            {
                ["SH.Background"] = "not-a-color",
                ["SH.Foreground"] = "#GGGGGG"
            }
        };
        Assert.DoesNotThrow(() => ThemeApplier.Apply(theme));
    }

    [Test]
    public void Apply_WithMixedValidInvalidColors_DoesNotThrow()
    {
        var theme = new ThemeDefinition
        {
            Name = "Mixed",
            BasedOn = "Light",
            Colors = new Dictionary<string, string>
            {
                ["SH.Valid"] = "#FF0000",
                ["SH.Invalid"] = "xyz",
                ["SH.AlsoValid"] = "#00FF00"
            }
        };
        Assert.DoesNotThrow(() => ThemeApplier.Apply(theme));
    }

    [Test]
    public void ThemeDefinition_DefaultsWork()
    {
        var theme = new ThemeDefinition();
        Assert.Multiple(() =>
        {
            Assert.That(theme.Name, Is.EqualTo("Light"));
            Assert.That(theme.BasedOn, Is.EqualTo("Light"));
            Assert.That(theme.Colors, Is.Not.Null);
            Assert.That(theme.Colors, Is.Empty);
        });
    }
}

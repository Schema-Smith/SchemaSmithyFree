using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace SchemaHammer.Themes;

public static class ThemeApplier
{
    public static void Apply(ThemeDefinition theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var isDark = theme.BasedOn == "Dark";
        app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        foreach (var (key, hexColor) in theme.Colors)
        {
            if (!Color.TryParse(hexColor, out var color))
                continue;

            // Set brush resource for general XAML binding
            app.Resources[key] = new SolidColorBrush(color);

            // Set color resource for gradient stops and other Color-typed references
            app.Resources[$"{key}.Color"] = color;
        }
    }

    public static WindowIcon? GetIcon(bool isDark)
    {
        // Icon assets will be wired up in a later task
        return null;
    }
}

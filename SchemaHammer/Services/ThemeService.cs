using SchemaHammer.Themes;

namespace SchemaHammer.Services;

public class ThemeService
{
    public static ThemeService? Instance { get; internal set; }

    public ThemeDefinition Current { get; private set; } = BuiltInThemes.Light;

    public List<ThemeDefinition> GetAvailableThemes() => [BuiltInThemes.Light, BuiltInThemes.Dark];

    public ThemeDefinition LoadTheme(string name) =>
        name == "Dark" ? BuiltInThemes.Dark : BuiltInThemes.Light;

    public event Action? ThemeChanged;

    public void SetActive(ThemeDefinition theme)
    {
        Current = theme;
        ThemeApplier.Apply(theme);
        ThemeChanged?.Invoke();
    }
}

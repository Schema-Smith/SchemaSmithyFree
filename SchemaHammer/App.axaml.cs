using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;
using SchemaHammer.Views;

namespace SchemaHammer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var themeService = new ThemeService();
            ThemeService.Instance = themeService;

            var settingsService = new UserSettingsService();
            settingsService.Load();

            var theme = themeService.LoadTheme(settingsService.Settings.ActiveThemeName);
            themeService.SetActive(theme);

            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel(settingsService);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SchemaHammer.Models;
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

            var productTreeService = new ProductTreeService();
            var editorService = new EditorService();
            var navigationService = new NavigationService();
            var schemaFileService = new SchemaFileService();

            var mainWindow = new MainWindow();
            var viewModel = new MainWindowViewModel(
                settingsService, navigationService, editorService, productTreeService, schemaFileService);
            mainWindow.DataContext = viewModel;

            TreeNodeModel.SetBusyCallback = busy =>
            {
                if (busy) MainWindow.SetBusy(mainWindow);
                else MainWindow.SetNormal(mainWindow);
            };

            desktop.MainWindow = mainWindow;

            viewModel.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

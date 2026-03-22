using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IUserSettingsService _settingsService;

    [ObservableProperty]
    private string _title = "SchemaHammer Community";

    [ObservableProperty]
    private string _productStatus = "NO PRODUCT";

    [ObservableProperty]
    private EditorBaseViewModel _currentEditor = new WelcomeViewModel();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    public UserSettings Settings => _settingsService.Settings;

    public MainWindowViewModel() : this(new UserSettingsService())
    {
    }

    public MainWindowViewModel(IUserSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void Exit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private void OpenThemeSettings()
    {
        var current = ThemeService.Instance?.Current;
        if (current == null) return;
        var next = current.BasedOn == "Dark" ? "Light" : "Dark";
        var theme = ThemeService.Instance!.LoadTheme(next);
        ThemeService.Instance.SetActive(theme);
        _settingsService.Settings.ActiveThemeName = theme.Name;
        _settingsService.Save();
    }

    public void SaveWindowState(bool isMaximized, int x, int y, double width, double height)
    {
        Settings.IsMaximized = isMaximized;
        Settings.WindowX = x;
        Settings.WindowY = y;
        Settings.WindowWidth = width;
        Settings.WindowHeight = height;
        _settingsService.Save();
    }
}

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IUserSettingsService _settingsService;
    private readonly INavigationService _navigationService;
    private readonly IEditorService _editorService;
    private readonly IProductTreeService _productTreeService;

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

    [ObservableProperty]
    private bool _canNavigateBack;

    public UserSettings Settings => _settingsService.Settings;
    public ProductTreeViewModel TreeViewModel { get; }
    public ObservableCollection<TreeNodeModel> NavigationHistory { get; } = [];

    private bool _suppressHistory;

    public MainWindowViewModel()
        : this(new UserSettingsService(), new NavigationService(), new EditorService(), new ProductTreeService())
    {
    }

    public MainWindowViewModel(
        IUserSettingsService settingsService,
        INavigationService navigationService,
        IEditorService editorService,
        IProductTreeService productTreeService)
    {
        _settingsService = settingsService;
        _navigationService = navigationService;
        _editorService = editorService;
        _productTreeService = productTreeService;

        TreeViewModel = new ProductTreeViewModel();
        TreeViewModel.NodeSelected += OnNodeSelected;
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
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

    [RelayCommand]
    private async Task ChooseProduct()
    {
        var topLevel = TopLevel.GetTopLevel(
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Choose Product", AllowMultiple = false });

        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;

        var productJsonPath = Path.Combine(path, "Product.json");
        if (!File.Exists(productJsonPath))
            return;

        LoadProductFromPath(path);
    }

    [RelayCommand]
    private void ReloadTree()
    {
        var roots = _productTreeService.ReloadProduct();
        TreeViewModel.SetRootNodes(roots);

        _navigationService.Clear();
        SyncNavigationHistory();

        CurrentEditor = new WelcomeViewModel();
    }

    [RelayCommand]
    private void NavigateBack()
    {
        var node = _navigationService.Pop();
        if (node == null) return;

        NavigateWithSuppression(node);
    }

    [RelayCommand]
    private void NavigateToHistory(int index)
    {
        var node = _navigationService.NavigateTo(index);
        if (node == null) return;

        NavigateWithSuppression(node);
    }

    [RelayCommand]
    private void LoadRecentProduct(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var productJsonPath = Path.Combine(path, "Product.json");
        if (!File.Exists(productJsonPath))
            return;

        LoadProductFromPath(path);
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

    private void OnNodeSelected(TreeNodeModel node, TreeNodeModel? previousNode)
    {
        if (!_suppressHistory && previousNode != null)
        {
            _navigationService.Push(previousNode);
            SyncNavigationHistory();
        }

        var editor = _editorService.GetEditor(node);
        if (editor != null)
            CurrentEditor = editor;

        _settingsService.Settings.LastSelectedNodePath = node.FullTreePath;
    }

    private void NavigateWithSuppression(TreeNodeModel node)
    {
        _suppressHistory = true;
        try
        {
            TreeViewModel.SelectedNode = node;
        }
        finally
        {
            _suppressHistory = false;
        }

        SyncNavigationHistory();
    }

    private void SyncNavigationHistory()
    {
        NavigationHistory.Clear();
        for (var i = _navigationService.History.Count - 1; i >= 0; i--)
            NavigationHistory.Add(_navigationService.History[i]);
        CanNavigateBack = _navigationService.CanGoBack;
    }

    internal void LoadProductFromPath(string path)
    {
        var roots = _productTreeService.LoadProduct(path);
        TreeViewModel.SetRootNodes(roots);

        _navigationService.Clear();
        SyncNavigationHistory();

        var product = _productTreeService.Product;
        Title = $"SchemaHammer Community — {product?.Name ?? "Unknown"}";
        ProductStatus = product?.Name ?? "NO PRODUCT";

        _settingsService.AddRecentProduct(path);
        _settingsService.Save();

        CurrentEditor = new WelcomeViewModel();

        var lastPath = _settingsService.Settings.LastSelectedNodePath;
        if (!string.IsNullOrEmpty(lastPath) && roots.Count > 0)
        {
            var node = roots[0].FindByTreePath(lastPath);
            if (node != null)
                TreeViewModel.SelectedNode = node;
        }
    }
}

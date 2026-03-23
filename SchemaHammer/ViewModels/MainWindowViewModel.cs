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
    private readonly ISchemaFileService _schemaFileService;

    [ObservableProperty]
    private string _title = "SchemaHammer Community";

    [ObservableProperty]
    private string _productStatus = "NO PRODUCT";

    [ObservableProperty]
    private string _productStatusTooltip = "";

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
        : this(new UserSettingsService(), new NavigationService(), new EditorService(), new ProductTreeService(), new SchemaFileService())
    {
    }

    public MainWindowViewModel(
        IUserSettingsService settingsService,
        INavigationService navigationService,
        IEditorService editorService,
        IProductTreeService productTreeService,
        ISchemaFileService schemaFileService)
    {
        _settingsService = settingsService;
        _navigationService = navigationService;
        _editorService = editorService;
        _productTreeService = productTreeService;
        _schemaFileService = schemaFileService;

        TreeViewModel = new ProductTreeViewModel();
        TreeViewModel.NodeSelected += OnNodeSelected;
    }

    public void Initialize()
    {
        var lastProduct = Settings.RecentProducts.FirstOrDefault();
        if (!string.IsNullOrEmpty(lastProduct) && Directory.Exists(lastProduct))
        {
            var productJsonPath = Path.Combine(lastProduct, "Product.json");
            if (File.Exists(productJsonPath))
                LoadProductFromPath(lastProduct);
        }
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

        var jsonFilter = new FilePickerFileType("Product File") { Patterns = ["Product.json"] };
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose Product",
                AllowMultiple = false,
                FileTypeFilter = [jsonFilter]
            });

        if (files.Count == 0) return;
        var filePath = files[0].Path.LocalPath;

        if (!Path.GetFileName(filePath).Equals("Product.json", StringComparison.OrdinalIgnoreCase))
            return;

        var productDir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(productDir)) return;

        LoadProductFromPath(productDir);
    }

    [RelayCommand]
    private void ReloadTree()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var childNodes = _productTreeService.ReloadProduct();
        sw.Stop();
        var product = _productTreeService.Product;

        var productNode = new TreeNodeModel
        {
            Text = product?.Name ?? "Product",
            Tag = "Product",
            NodePath = Path.GetDirectoryName(product?.FilePath) ?? "",
            ImageKey = "product",
            IsExpanded = true
        };

        foreach (var child in childNodes)
        {
            child.Parent = productNode;
            productNode.Children.Add(child);
        }

        TreeViewModel.SetRootNodes([productNode]);

        _navigationService.Clear();
        SyncNavigationHistory();

        CurrentEditor = new WelcomeViewModel();

        var productPath = Path.GetDirectoryName(product?.FilePath) ?? "";
        ProductStatusTooltip = $"{productPath} [Loaded {(_productTreeService.SearchList?.Count ?? 0):N0} nodes in {sw.ElapsedMilliseconds:N0}ms]";
    }

    [RelayCommand]
    private async Task UpdateSchemaFiles()
    {
        if (_productTreeService.Product == null) return;

        var productPath = Path.GetDirectoryName(_productTreeService.Product.FilePath);
        if (string.IsNullOrEmpty(productPath)) return;

        try
        {
            var count = _schemaFileService.UpdateSchemaFiles(productPath);
            var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                "Update Schema Files",
                $"Updated {count} schema files in .json-schemas/",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Info);
            await box.ShowAsync();
        }
        catch (Exception ex)
        {
            var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                "Update Schema Files",
                $"Error: {ex.Message}",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error);
            await box.ShowAsync();
        }
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

    public void SelectNodeFromSearch(TreeNodeModel node)
    {
        TreeViewModel.SelectedNode = node;
    }

    [RelayCommand]
    private void SearchTree()
    {
        OpenSearchRequested?.Invoke("Tree");
    }

    [RelayCommand]
    private void SearchCode()
    {
        OpenSearchRequested?.Invoke("Code");
    }

    public event Action<string>? OpenSearchRequested;
    public event Action? ShowAboutRequested;

    [RelayCommand]
    private void ShowAbout()
    {
        ShowAboutRequested?.Invoke();
    }

    internal IProductTreeService ProductTreeService => _productTreeService;

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
        {
            editor.NavigateToNode = targetNode => SelectNodeFromSearch(targetNode);
            CurrentEditor = editor;
        }

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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var childNodes = _productTreeService.LoadProduct(path);
        sw.Stop();
        var product = _productTreeService.Product;

        var productNode = new TreeNodeModel
        {
            Text = product?.Name ?? Path.GetFileName(path),
            Tag = "Product",
            NodePath = path,
            ImageKey = "product",
            IsExpanded = true
        };

        foreach (var child in childNodes)
        {
            child.Parent = productNode;
            productNode.Children.Add(child);
        }

        TreeViewModel.SetRootNodes([productNode]);

        _navigationService.Clear();
        SyncNavigationHistory();

        Title = $"SchemaHammer Community — {product?.Name ?? "Unknown"}";
        ProductStatus = product?.Name ?? "NO PRODUCT";
        ProductStatusTooltip = $"{path} [Loaded {(_productTreeService.SearchList?.Count ?? 0):N0} nodes in {sw.ElapsedMilliseconds:N0}ms]";

        _settingsService.AddRecentProduct(path);
        _settingsService.Save();

        CurrentEditor = new WelcomeViewModel();

        var lastPath = _settingsService.Settings.LastSelectedNodePath;
        if (!string.IsNullOrEmpty(lastPath))
        {
            var node = productNode.FindByTreePath(lastPath);
            if (node != null)
                TreeViewModel.SelectedNode = node;
        }
    }
}

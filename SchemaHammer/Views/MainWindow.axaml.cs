using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using SchemaHammer.ViewModels;

namespace SchemaHammer.Views;

public partial class MainWindow : Window
{
    private static readonly Cursor WaitCursor = new(StandardCursorType.Wait);

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel vm)
        {
            RestoreWindowState(vm);
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsBusy) && sender is MainWindowViewModel vm)
        {
            Cursor = vm.IsBusy ? WaitCursor : Cursor.Default;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (DataContext is MainWindowViewModel vm)
        {
            // Don't save minimized state — keep previous normal bounds
            if (WindowState == WindowState.Minimized) return;

            if (vm.TreeViewModel.SelectedNode != null)
                vm.Settings.LastSelectedNodePath = vm.TreeViewModel.SelectedNode.FullTreePath;

            vm.SaveWindowState(
                WindowState == WindowState.Maximized,
                Position.X,
                Position.Y,
                Width,
                Height);
        }
    }

    public static void SetBusy(Window window)
    {
        window.Cursor = WaitCursor;
    }

    public static void SetNormal(Window window)
    {
        window.Cursor = Cursor.Default;
    }

    private void HistoryListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedIndex < 0) return;
        if (DataContext is not MainWindowViewModel vm) return;

        var historyIndex = vm.NavigationHistory.Count - 1 - listBox.SelectedIndex;
        vm.NavigateToHistoryCommand.Execute(historyIndex);

        listBox.SelectedIndex = -1;

        // Close the flyout
        if (listBox.Parent is FlyoutPresenter presenter)
        {
            var flyout = presenter.Parent;
            if (flyout is Popup popup)
                popup.Close();
        }
    }

    private void RestoreWindowState(MainWindowViewModel vm)
    {
        var settings = vm.Settings;

        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = Math.Max(settings.WindowWidth, MinWidth);
            Height = Math.Max(settings.WindowHeight, MinHeight);

            if (settings.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                var screen = Screens.Primary;
                if (screen != null)
                {
                    var bounds = screen.WorkingArea;
                    var x = Math.Max(0, Math.Min(settings.WindowX, bounds.Width - Width));
                    var y = Math.Max(0, Math.Min(settings.WindowY, bounds.Height - Height));
                    Position = new PixelPoint((int)x, (int)y);
                }
                else
                {
                    Position = new PixelPoint((int)settings.WindowX, (int)settings.WindowY);
                }
            }
        }
    }
}

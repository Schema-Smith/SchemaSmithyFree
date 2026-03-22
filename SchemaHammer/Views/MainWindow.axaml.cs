using Avalonia;
using Avalonia.Controls;
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

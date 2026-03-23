// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SchemaHammer.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var link = this.FindControl<TextBlock>("GitHubLink");
        if (link != null)
            link.PointerPressed += OnLinkPressed;
    }

    private void OnLinkPressed(object? sender, PointerPressedEventArgs e)
    {
        var url = (DataContext as SchemaHammer.ViewModels.AboutViewModel)?.GitHubUrl;
        if (!string.IsNullOrEmpty(url))
            OpenUrl(url);
    }

    private static void OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}

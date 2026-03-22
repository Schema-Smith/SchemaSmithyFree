using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using SchemaHammer.Views;

namespace SchemaHammer.UnitTests;

public class ViewInitializationTests
{
    [AvaloniaTest]
    public void MainWindow_CanInitialize()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(window, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void ProductTreeView_CanInitialize()
    {
        var view = new ProductTreeView();
        var window = new Window { Content = view, Width = 400, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }
}

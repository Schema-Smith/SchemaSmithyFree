// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NSubstitute;
using SchemaHammer.ViewModels;
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

    [AvaloniaTest]
    public void SearchWindow_CanInitialize()
    {
        var searchWindow = new SearchWindow();
        searchWindow.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(searchWindow, Is.Not.Null);
        searchWindow.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_WithDataContext_CanInitialize()
    {
        var treeService = NSubstitute.Substitute.For<SchemaHammer.Services.IProductTreeService>();
        treeService.SearchList.Returns(new System.Collections.Generic.List<SchemaHammer.Models.TreeNodeModel>());
        treeService.Templates.Returns(new System.Collections.Generic.Dictionary<string, Schema.Domain.Template>());
        var vm = new SearchViewModel(treeService);
        var searchWindow = new SearchWindow { DataContext = vm };
        searchWindow.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(searchWindow.DataContext, Is.SameAs(vm));
        searchWindow.Close();
    }

    [AvaloniaTest]
    public void AboutWindow_CanInitialize()
    {
        var aboutWindow = new AboutWindow();
        aboutWindow.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(aboutWindow, Is.Not.Null);
        aboutWindow.Close();
    }

    [AvaloniaTest]
    public void AboutWindow_WithDataContext_CanInitialize()
    {
        var vm = new AboutViewModel();
        var aboutWindow = new AboutWindow { DataContext = vm };
        aboutWindow.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(aboutWindow.DataContext, Is.SameAs(vm));
        aboutWindow.Close();
    }
}

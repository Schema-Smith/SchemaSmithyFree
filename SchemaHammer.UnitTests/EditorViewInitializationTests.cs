// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.UnitTests;

public class EditorViewInitializationTests
{
    [AvaloniaTest]
    public void WelcomeView_CanInitialize()
    {
        var view = new WelcomeView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void ContainerEditorView_CanInitialize()
    {
        var view = new ContainerEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void TableEditorView_CanInitialize()
    {
        var view = new TableEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void ColumnEditorView_CanInitialize()
    {
        var view = new ColumnEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void IndexEditorView_CanInitialize()
    {
        var view = new IndexEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void ForeignKeyEditorView_CanInitialize()
    {
        var view = new ForeignKeyEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void CheckConstraintEditorView_CanInitialize()
    {
        var view = new CheckConstraintEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void StatisticEditorView_CanInitialize()
    {
        var view = new StatisticEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void XmlIndexEditorView_CanInitialize()
    {
        var view = new XmlIndexEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void FullTextIndexEditorView_CanInitialize()
    {
        var view = new FullTextIndexEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void IndexedViewEditorView_CanInitialize()
    {
        var view = new IndexedViewEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void ProductEditorView_CanInitialize()
    {
        var view = new ProductEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void TemplateEditorView_CanInitialize()
    {
        var view = new TemplateEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void SqlScriptEditorView_CanInitialize()
    {
        var view = new SqlScriptEditorView();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(view, Is.Not.Null);
        window.Close();
    }
}

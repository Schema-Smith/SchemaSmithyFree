// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Dialogs;

public abstract class DialogTestBase
{
    protected static Window HostDialog(Window dialog)
    {
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return dialog;
    }

    protected static T? FindControl<T>(Window dialog, string name) where T : Control
        => dialog.FindControl<T>(name);

    protected static T? FindDescendant<T>(Window dialog) where T : Control
        => dialog.GetVisualDescendants().OfType<T>().FirstOrDefault();

    protected static List<T> FindDescendants<T>(Window dialog) where T : Control
        => dialog.GetVisualDescendants().OfType<T>().ToList();
}

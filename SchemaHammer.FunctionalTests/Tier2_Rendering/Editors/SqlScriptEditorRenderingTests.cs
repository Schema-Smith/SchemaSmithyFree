// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: SqlScriptEditorRenderingTests.cs. Excluded: none applicable.

using Avalonia.Headless.NUnit;
using SchemaHammer.Controls;
using SchemaHammer.Views.Editors;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Editors;

[TestFixture]
public class SqlScriptEditorRenderingTests : RenderingTestBase
{
    [AvaloniaTest]
    public void SqlScriptEditorView_RendersWithoutError()
    {
        var vm = GetSqlScriptEditor();
        var view = new SqlScriptEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(view.IsVisible, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public void SqlScriptEditorView_BindingsReflectViewModel()
    {
        var vm = GetSqlScriptEditor();
        var view = new SqlScriptEditorView { DataContext = vm };
        var window = HostView(view);

        Assert.That(vm.EditorTitle, Is.Not.Null.And.Not.Empty);
        Assert.That(vm.DisplayContent, Is.Not.Null);

        window.Close();
    }

    [AvaloniaTest]
    public void SqlScriptEditorView_SqlEditorControlExists()
    {
        var vm = GetSqlScriptEditor();
        var view = new SqlScriptEditorView { DataContext = vm };
        var window = HostView(view);

        var sqlEditor = FindControl<SqlEditorControl>(view, "SqlEditor");
        Assert.That(sqlEditor, Is.Not.Null, "SqlEditor named control should be present");

        window.Close();
    }

    [AvaloniaTest]
    public void SqlScriptEditorView_FindBarHiddenByDefault()
    {
        var vm = GetSqlScriptEditor();
        var view = new SqlScriptEditorView { DataContext = vm };
        var window = HostView(view);

        var findBar = FindControl<FindBarControl>(view, "FindBar");
        Assert.That(findBar, Is.Not.Null, "FindBar named control should be present");
        // FindBar is hidden until Ctrl+F is pressed
        Assert.That(findBar!.IsVisible, Is.False, "FindBar should be hidden by default");

        window.Close();
    }
}

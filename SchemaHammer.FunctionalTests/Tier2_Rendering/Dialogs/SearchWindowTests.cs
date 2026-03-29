// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise exclusions: AutoSearchCheckBox (not in Community), RegexOn/Off, SearchTypeHidden/Visible,
// ItemTypeFilter_Populates (all Enterprise-only features).

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;
using SchemaHammer.Views;

namespace SchemaHammer.FunctionalTests.Tier2_Rendering.Dialogs;

[TestFixture]
public class SearchWindowTests : DialogTestBase
{
    private string _tempDir = string.Empty;
    private ProductTreeService _treeService = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SchemaHammerFT_Search_" + Guid.NewGuid().ToString("N"));
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Users]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithColumn("[Name]", "nvarchar(100)")))
            .Build(_tempDir);

        _treeService = new ProductTreeService();
        _treeService.LoadProduct(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best effort — ignore cleanup failures
        }
    }

    [AvaloniaTest]
    public void SearchWindow_RendersWithoutError()
    {
        var vm = new SearchViewModel(_treeService);
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        Assert.That(dialog.IsVisible, Is.True);

        dialog.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_TwoTabs_Exist()
    {
        var vm = new SearchViewModel(_treeService);
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        var tabControl = FindDescendant<TabControl>(dialog);
        Assert.That(tabControl, Is.Not.Null, "TabControl not found");
        Assert.That(tabControl!.ItemCount, Is.EqualTo(2));

        dialog.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_TreeResultsGrid_Exists()
    {
        var vm = new SearchViewModel(_treeService);
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        var grid = FindControl<DataGrid>(dialog, "TreeResultsGrid");
        Assert.That(grid, Is.Not.Null, "TreeResultsGrid not found");

        dialog.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_CodeSearchBox_Exists()
    {
        var vm = new SearchViewModel(_treeService);
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        var searchBox = FindControl<TextBox>(dialog, "CodeSearchBox");
        Assert.That(searchBox, Is.Not.Null, "CodeSearchBox not found");

        dialog.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_SearchTypeCombo_Populates()
    {
        var vm = new SearchViewModel(_treeService);
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        // The ComboBox is on the Tree tab (active by default). Verify via the ViewModel's
        // SearchTypes collection which backs the ComboBox — must have 3+ items
        // (Contains, Begins With, Ends With).
        Assert.That(vm.SearchTypes, Is.Not.Empty, "SearchTypes collection is empty");
        Assert.That(vm.SearchTypes.Count, Is.GreaterThanOrEqualTo(3), "Expected at least 3 search types");

        dialog.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_CodeResultsGrid_Exists()
    {
        var vm = new SearchViewModel(_treeService);
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        var grid = FindControl<DataGrid>(dialog, "CodeResultsGrid");
        Assert.That(grid, Is.Not.Null, "CodeResultsGrid not found");

        dialog.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_EnterKey_TriggersCodeSearch()
    {
        var vm = new SearchViewModel(_treeService, "Code");
        vm.CodeSearchTerm = "Users";
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        dialog.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        dialog.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.That(vm.CodeSearchResults, Is.Not.Empty,
            "Enter key on code search tab should trigger search");

        dialog.Close();
    }

    [AvaloniaTest]
    public void SearchWindow_EscapeKey_ClosesWindow()
    {
        var vm = new SearchViewModel(_treeService);
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);
        Assert.That(dialog.IsVisible, Is.True);

        dialog.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.That(dialog.IsVisible, Is.False,
            "Escape key should close the search window");
    }

    [AvaloniaTest]
    public void SearchWindow_EnterKey_OnTreeTab_DoesNotTriggerCodeSearch()
    {
        var vm = new SearchViewModel(_treeService);
        vm.CodeSearchTerm = "Users";
        var dialog = new SearchWindow { DataContext = vm };
        HostDialog(dialog);

        dialog.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        dialog.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.That(vm.CodeSearchResults, Is.Empty,
            "Enter key on tree tab should not trigger code search");

        dialog.Close();
    }
}

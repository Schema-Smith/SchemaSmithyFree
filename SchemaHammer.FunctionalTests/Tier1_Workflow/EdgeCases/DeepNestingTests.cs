// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: none — Community-only edge-case tests.

using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;

namespace SchemaHammer.FunctionalTests.Tier1_Workflow.EdgeCases;

[TestFixture]
public class DeepNestingTests : TestProductFixture
{
    [Test]
    public void DeepNestedScriptFolders_LoadSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithScript("Views", "vw_Top.sql", "SELECT 1"))
            .Build(TempDir);

        // Create a deeply nested script manually
        var baseDir = Path.Combine(TempDir, "Templates", "Main", "Procedures");
        var nestedPath = Path.Combine(baseDir, "Sub1", "Sub2", "Sub3");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "deep.sql"), "SELECT 1");

        var svc = new ProductTreeService();
        Assert.DoesNotThrow(() => svc.LoadProduct(TempDir), "Deep nested folders should load without error");
    }

    [Test]
    public void DeepNestedScriptFolders_ScriptAppearsInTree()
    {
        new TestProductBuilder()
            .WithTemplate("Main", _ => { })
            .Build(TempDir);

        var baseDir = Path.Combine(TempDir, "Templates", "Main", "Procedures");
        var nestedPath = Path.Combine(baseDir, "Sub1", "Sub2", "Sub3");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "deep.sql"), "SELECT deep FROM nested");

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var deepNode = roots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Equals("deep.sql", StringComparison.OrdinalIgnoreCase));

        Assert.That(deepNode, Is.Not.Null, "Script at deep nesting level should appear in tree");
    }

    [Test]
    public void FiftyTables_LoadSuccessfully()
    {
        var builder = new TestProductBuilder();
        builder.WithTemplate("Main", t =>
        {
            for (var i = 1; i <= 50; i++)
                t.WithTable($"[dbo].[Table{i:D2}]", table => table.WithColumn("[Id]", "int"));
        });
        builder.Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var tablesNode = AssertionHelpers.FindNode(roots, "Templates/Main/Tables");
        Assert.That(tablesNode, Is.Not.Null);
        tablesNode!.EnsureExpanded();

        Assert.That(tablesNode.Children.Count, Is.EqualTo(50),
            "All 50 tables should load");
    }

    [Test]
    public void TableWith50Columns_LoadsSuccessfully()
    {
        var builder = new TestProductBuilder();
        builder.WithTemplate("Main", t =>
        {
            t.WithTable("[dbo].[WideTable]", table =>
            {
                for (var i = 1; i <= 50; i++)
                    table.WithColumn($"[Col{i:D2}]", "nvarchar(100)");
            });
        });
        builder.Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var tableNode = AssertionHelpers.FindNode(roots, "Templates/Main/Tables/[dbo].[WideTable]");
        Assert.That(tableNode, Is.Not.Null);
        tableNode!.EnsureExpanded();

        var columnsContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Columns", StringComparison.OrdinalIgnoreCase));
        Assert.That(columnsContainer, Is.Not.Null, "Columns container should exist");
        Assert.That(columnsContainer!.Children.Count, Is.EqualTo(50),
            "All 50 columns should be loaded");
    }

    [Test]
    public void Navigation_WorksWithManyChildTypes()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[RichTable]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithColumn("[Name]", "nvarchar(100)")
                    .WithIndex("[IX_RichTable_Name]", "[Name]")
                    .WithClusteredIndex("[PK_RichTable]", "[Id]")
                    .WithForeignKey("[FK_RichTable_Other]", "[Id]", "[dbo].[Other]", "[Id]")
                    .WithCheckConstraint("[CK_RichTable_Name]", "[Name] IS NOT NULL")
                    .WithStatistic("[ST_RichTable_Name]", "[Name]")))
            .Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var tableNode = AssertionHelpers.FindNode(roots, "Templates/Main/Tables/[dbo].[RichTable]");
        Assert.That(tableNode, Is.Not.Null);
        tableNode!.EnsureExpanded();

        var childNames = tableNode.Children.Select(c => c.Text).ToList();
        Assert.That(childNames, Has.Some.EqualTo("Columns").IgnoreCase);
        Assert.That(childNames, Has.Some.EqualTo("Indexes").IgnoreCase);
        Assert.That(childNames, Has.Some.EqualTo("Foreign Keys").IgnoreCase);
        Assert.That(childNames, Has.Some.EqualTo("Check Constraints").IgnoreCase);
        Assert.That(childNames, Has.Some.EqualTo("Statistics").IgnoreCase);
    }

    [Test]
    public void CodeSearch_FindsScriptsAtAllNestingLevels()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithScript("Views", "vw_Top.sql", "SELECT TopLevel FROM T"))
            .Build(TempDir);

        // Create nested script
        var nestedPath = Path.Combine(TempDir, "Templates", "Main", "Procedures", "Nested");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "sp_Nested.sql"), "SELECT NestedLevel FROM T");

        var svc = new ProductTreeService();
        svc.LoadProduct(TempDir);
        foreach (var node in svc.SearchList.ToList()) node.EnsureExpanded();

        var vm = new SearchViewModel(svc);
        vm.CodeSearchTerm = "Level";
        vm.SearchCodeCommand.Execute(null);

        var scriptResults = vm.CodeSearchResults.Where(r => r.Type == "Sql Script").ToList();
        Assert.That(scriptResults.Count, Is.GreaterThanOrEqualTo(2),
            "Code search should find scripts at multiple nesting levels");
    }

    [Test]
    public void TableWithManyIndexes_LoadsSuccessfully()
    {
        var builder = new TestProductBuilder();
        builder.WithTemplate("Main", t =>
        {
            t.WithTable("[dbo].[IndexHeavy]", table =>
            {
                table.WithColumn("[Id]", "int", nullable: false);
                for (var i = 1; i <= 10; i++)
                {
                    table.WithColumn($"[Col{i}]", "nvarchar(100)");
                    table.WithIndex($"[IX_IndexHeavy_Col{i}]", $"[Col{i}]");
                }
            });
        });
        builder.Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var tableNode = AssertionHelpers.FindNode(roots, "Templates/Main/Tables/[dbo].[IndexHeavy]");
        Assert.That(tableNode, Is.Not.Null);
        tableNode!.EnsureExpanded();

        var indexesContainer = tableNode.Children
            .FirstOrDefault(c => c.Text.Equals("Indexes", StringComparison.OrdinalIgnoreCase));
        Assert.That(indexesContainer, Is.Not.Null, "Indexes container should exist");
        Assert.That(indexesContainer!.Children.Count, Is.EqualTo(10),
            "All 10 indexes should be loaded");
    }

    [Test]
    public void MultipleTemplates_AllLoadSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[MainTable]", table => table.WithColumn("[Id]", "int")))
            .WithTemplate("Secondary", t => t
                .WithTable("[dbo].[SecondaryTable]", table => table.WithColumn("[Id]", "int")))
            .WithTemplate("Tertiary", t => t
                .WithTable("[dbo].[TertiaryTable]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var templatesNode = roots.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesNode, Is.Not.Null);
        templatesNode!.EnsureExpanded();

        var templateNames = templatesNode.Children.Select(c => c.Text).ToList();
        Assert.That(templateNames, Has.Some.EqualTo("Main").IgnoreCase);
        Assert.That(templateNames, Has.Some.EqualTo("Secondary").IgnoreCase);
        Assert.That(templateNames, Has.Some.EqualTo("Tertiary").IgnoreCase);
    }

    [Test]
    public void ManyScriptFolders_AllLoadSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithScript("Views", "vw_A.sql", "SELECT 1")
                .WithScript("Views", "vw_B.sql", "SELECT 2")
                .WithScript("Views", "vw_C.sql", "SELECT 3"))
            .Build(TempDir);

        // Add more script folders manually (simulate Procedures, Functions, Triggers etc.)
        var templateDir = Path.Combine(TempDir, "Templates", "Main");
        foreach (var folder in new[] { "Functions", "Triggers", "DDLTriggers" })
        {
            var folderPath = Path.Combine(templateDir, folder);
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(Path.Combine(folderPath, $"script_{folder}.sql"), $"SELECT '{folder}'");
        }

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var allNodes = roots.SelectMany(AssertionHelpers.FindAllNodes).ToList();

        Assert.That(allNodes.Any(n => n.Text.Equals("Views", StringComparison.OrdinalIgnoreCase)),
            Is.True, "Views folder should appear");
        Assert.That(allNodes.Any(n => n.Text.Equals("Functions", StringComparison.OrdinalIgnoreCase)),
            Is.True, "Functions folder should appear");
        Assert.That(allNodes.Any(n => n.Text.Equals("Triggers", StringComparison.OrdinalIgnoreCase)),
            Is.True, "Triggers folder should appear");
    }
}

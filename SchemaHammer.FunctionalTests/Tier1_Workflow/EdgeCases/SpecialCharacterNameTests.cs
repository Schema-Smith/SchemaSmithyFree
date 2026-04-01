// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: none — Community-only edge-case tests.

using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Models;
using SchemaHammer.Services;

namespace SchemaHammer.FunctionalTests.Tier1_Workflow.EdgeCases;

[TestFixture]
public class SpecialCharacterNameTests : TestProductFixture
{
    private static List<TreeNodeModel> LoadTree(string dir)
    {
        var svc = new ProductTreeService();
        return svc.LoadProduct(dir);
    }

    private static IEnumerable<TreeNodeModel> AllNodes(List<TreeNodeModel> roots) =>
        roots.SelectMany(AssertionHelpers.FindAllNodes);

    [Test]
    public void Table_WithSpacesInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[User Profiles]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("User Profiles", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table with spaces in name should load");
    }

    [Test]
    public void Table_WithDotsInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[v1.0.Users]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("v1.0.Users", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table with dots in name should load");
    }

    [Test]
    public void Table_WithParenthesesInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Orders(Legacy)]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("Orders(Legacy)", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table with parentheses in name should load");
    }

    [Test]
    public void Table_WithAmpersandInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Sales&Marketing]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("Sales&Marketing", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table with ampersand in name should load");
    }

    [Test]
    public void Table_WithUnicodeInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Über_Données]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("Über_Données", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table with unicode characters in name should load");
    }

    [Test]
    public void Table_WithApostropheInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[O'Brien_Data]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("O'Brien", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table with apostrophe in name should load");
    }

    [Test]
    public void Column_WithSpecialCharsInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Users]", table => table
                    .WithColumn("[First Name]", "nvarchar(100)")
                    .WithColumn("[E-Mail Address]", "nvarchar(255)")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var allNodes = AllNodes(roots).ToList();

        Assert.That(allNodes.Any(n => n.Text.Contains("First Name", StringComparison.OrdinalIgnoreCase)),
            Is.True, "Column with space in name should appear in tree");
        Assert.That(allNodes.Any(n => n.Text.Contains("E-Mail Address", StringComparison.OrdinalIgnoreCase)),
            Is.True, "Column with hyphen and space should appear in tree");
    }

    [Test]
    public void Index_WithSpecialCharsInName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Users]", table => table
                    .WithColumn("[Id]", "int")
                    .WithIndex("[IX_Users_Special#Index]", "[Id]")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("IX_Users_Special", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Index with special chars in name should load");
    }

    [Test]
    public void Script_WithSpecialCharsInFileName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithScript("Views", "vw_Special-View (v2).sql", "SELECT 1"))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("vw_Special-View", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Script with special chars in filename should load");
    }

    [Test]
    public void TreeSearch_FindsSpecialCharacterNodes()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[User Profiles]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var svc = new ProductTreeService();
        svc.LoadProduct(TempDir);
        // Expand to populate SearchList
        foreach (var node in svc.SearchList.ToList()) node.EnsureExpanded();

        var vm = new SchemaHammer.ViewModels.SearchViewModel(svc);
        vm.TreeSearchTerm = "User Profiles";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Is.Not.Empty,
            "Tree search should find table with spaces in name");
    }

    [Test]
    public void Table_WithMaxLengthName_LoadsSuccessfully()
    {
        // SQL Server max identifier length is 128 characters
        var longName = new string('A', 128);
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable($"[dbo].[{longName}]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains(longName, StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table with 128-character name should load");
    }

    [Test]
    public void Table_WithReservedSqlWordAsName_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Select]", table => table.WithColumn("[Id]", "int")))
            .Build(TempDir);

        var roots = LoadTree(TempDir);
        var node = AllNodes(roots).FirstOrDefault(n =>
            n.Text.Contains("Select", StringComparison.OrdinalIgnoreCase));

        Assert.That(node, Is.Not.Null, "Table named after a SQL reserved word should load");
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
// Enterprise equivalent: none — Community-only edge-case tests.

using SchemaHammer.FunctionalTests.Fixtures;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.FunctionalTests.Tier1_Workflow.EdgeCases;

[TestFixture]
public class EmptyProductTests : TestProductFixture
{
    [Test]
    public void Product_ZeroTemplates_LoadsWithoutCrash()
    {
        new TestProductBuilder()
            .WithName("EmptyProduct")
            .Build(TempDir);

        var svc = new ProductTreeService();
        Assert.DoesNotThrow(() => svc.LoadProduct(TempDir));
    }

    [Test]
    public void Product_ZeroTemplates_RootsContainsTemplatesContainer()
    {
        new TestProductBuilder()
            .WithName("EmptyProduct")
            .Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var templatesNode = roots.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesNode, Is.Not.Null,
            "Templates container should always appear in tree roots even with zero templates");
    }

    [Test]
    public void Template_WithNoTables_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", _ => { })
            .Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var templateNode = AssertionHelpers.FindNode(roots, "Templates/Main");
        Assert.That(templateNode, Is.Not.Null, "Template with no tables should still appear");
    }

    [Test]
    public void Template_WithEmptyTablesFolder_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", _ => { })
            .Build(TempDir);

        var svc = new ProductTreeService();
        Assert.DoesNotThrow(() => svc.LoadProduct(TempDir));
    }

    [Test]
    public void Table_WithNoColumns_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[EmptyTable]"))
            .Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var allNodes = roots.SelectMany(AssertionHelpers.FindAllNodes);
        var tableNode = allNodes.FirstOrDefault(n =>
            n.Text.Contains("EmptyTable", StringComparison.OrdinalIgnoreCase));

        Assert.That(tableNode, Is.Not.Null, "Table with no columns should appear in tree");
    }

    [Test]
    public void Table_WithOneColumn_LoadsSuccessfully()
    {
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[MinimalTable]", table => table
                    .WithColumn("[Id]", "int", nullable: false)))
            .Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var allNodes = roots.SelectMany(AssertionHelpers.FindAllNodes);
        var tableNode = allNodes.FirstOrDefault(n =>
            n.Text.Contains("MinimalTable", StringComparison.OrdinalIgnoreCase));

        Assert.That(tableNode, Is.Not.Null, "Minimal table with one column should appear");
    }

    [Test]
    public void EmptyProduct_EditorService_OpensContainerEditor()
    {
        new TestProductBuilder()
            .WithName("EmptyProduct")
            .Build(TempDir);

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);
        var editorService = new EditorService();

        var templatesNode = roots.FirstOrDefault(n => n.Tag == "Templates");
        Assert.That(templatesNode, Is.Not.Null);

        var editor = editorService.GetEditor(templatesNode!);
        Assert.That(editor, Is.InstanceOf<ContainerEditorViewModel>(),
            "Templates container should open ContainerEditorViewModel");
    }

    [Test]
    public void EmptyProduct_SearchReturnsEmpty()
    {
        new TestProductBuilder()
            .WithName("EmptyProduct")
            .Build(TempDir);

        var svc = new ProductTreeService();
        svc.LoadProduct(TempDir);

        var vm = new SearchViewModel(svc);
        vm.TreeSearchTerm = "anything";
        vm.SearchTreeCommand.Execute(null);

        Assert.That(vm.TreeSearchResults, Is.Empty,
            "Search on a product with no tables should return no results");
    }

    [Test]
    public void EmptyScriptFolder_DoesNotAppearInTree()
    {
        // Script folders with no .sql files should be omitted from the tree
        new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithScript("Views", "vw_Something.sql", "SELECT 1"))
            .Build(TempDir);

        // Create an additional empty folder manually — should not appear
        Directory.CreateDirectory(Path.Combine(TempDir, "Templates", "Main", "Procedures"));

        var svc = new ProductTreeService();
        var roots = svc.LoadProduct(TempDir);

        var proceduresNode = roots.SelectMany(AssertionHelpers.FindAllNodes)
            .FirstOrDefault(n => n.Text.Equals("Procedures", StringComparison.OrdinalIgnoreCase));

        Assert.That(proceduresNode, Is.Null,
            "Empty script folders (no .sql files) should not appear in tree");
    }
}

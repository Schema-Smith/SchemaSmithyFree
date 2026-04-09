// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Linq;
using NUnit.Framework;
using Schema.Domain;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class ProductQuenchTests
{
    #region Platform Dispatch - Init Database

    [Test]
    public void GetInitDatabase_SqlServer_ReturnsMaster()
    {
        Assert.That(ProductQuench.GetInitDatabase(Platform.SqlServer), Is.EqualTo("master"));
    }

    [Test]
    public void GetInitDatabase_PostgreSQL_ReturnsPostgres()
    {
        Assert.That(ProductQuench.GetInitDatabase(Platform.PostgreSQL), Is.EqualTo("postgres"));
    }

    [Test]
    public void GetInitDatabase_MySQL_ReturnsInformationSchema()
    {
        Assert.That(ProductQuench.GetInitDatabase(Platform.MySQL), Is.EqualTo("information_schema"));
    }

    [Test]
    public void GetInitDatabase_InvalidPlatform_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductQuench.GetInitDatabase((Platform)999));
    }

    #endregion

    #region Platform Dispatch - Server ID Query

    [Test]
    public void GetServerIdQuery_SqlServer_ReturnsServerName()
    {
        Assert.That(ProductQuench.GetServerIdQuery(Platform.SqlServer), Is.EqualTo("SELECT @@SERVERNAME"));
    }

    [Test]
    public void GetServerIdQuery_PostgreSQL_ReturnsInetServerAddr()
    {
        Assert.That(ProductQuench.GetServerIdQuery(Platform.PostgreSQL), Is.EqualTo("SELECT inet_server_addr();"));
    }

    [Test]
    public void GetServerIdQuery_MySQL_ReturnsHostname()
    {
        Assert.That(ProductQuench.GetServerIdQuery(Platform.MySQL), Is.EqualTo("SELECT @@hostname"));
    }

    [Test]
    public void GetServerIdQuery_InvalidPlatform_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductQuench.GetServerIdQuery((Platform)999));
    }

    #endregion

    #region SpecialTokenTags Tests

    [Test]
    public void SpecialTokenTags_ContainsMaterializedViewSchema()
    {
        Assert.That(ProductQuench.SpecialTokenTags, Does.Contain("MaterializedViewSchema_"));
    }

    [Test]
    public void SpecialTokenTags_ContainsAllExpectedTags()
    {
        Assert.That(ProductQuench.SpecialTokenTags, Is.EquivalentTo(
            new[] { "TableSchema_", "ObjectScripts_", "QueryTokens_", "MaterializedViewSchema_", "IndexedViewSchema_" }));
    }

    [Test]
    public void SpecialTokenTags_ContainsIndexedViewSchema()
    {
        Assert.That(ProductQuench.SpecialTokenTags, Does.Contain("IndexedViewSchema_"));
    }

    #endregion

    #region BuildSpecialTokens Tests

    [Test]
    public void BuildSpecialTokens_IncludesMaterializedViewSchemaToken()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens.ContainsKey("MaterializedViewSchema_Core"), Is.True);
    }

    [Test]
    public void BuildSpecialTokens_MaterializedViewSchema_DefaultsToEmptyArray()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["MaterializedViewSchema_Core"], Is.EqualTo("[]"));
    }

    [Test]
    public void BuildSpecialTokens_MaterializedViewSchema_EscapesSingleQuotes()
    {
        var template = new Template { Name = "Core", MaterializedViewSchema = "[{\"Name\":\"test's view\"}]" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["MaterializedViewSchema_Core"], Is.EqualTo("[{\"Name\":\"test''s view\"}]"));
    }

    [Test]
    public void BuildSpecialTokens_IncludesAllFiveTokenTypes()
    {
        var template = new Template { Name = "MyTemplate" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens.Keys, Does.Contain("TableSchema_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("ObjectScripts_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("QueryTokens_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("MaterializedViewSchema_MyTemplate"));
        Assert.That(tokens.Keys, Does.Contain("IndexedViewSchema_MyTemplate"));
        Assert.That(tokens, Has.Count.EqualTo(5));
    }

    [Test]
    public void BuildSpecialTokens_IncludesIndexedViewSchemaToken()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens.ContainsKey("IndexedViewSchema_Core"), Is.True);
    }

    [Test]
    public void BuildSpecialTokens_IndexedViewSchema_DefaultsToEmptyArray()
    {
        var template = new Template { Name = "Core" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["IndexedViewSchema_Core"], Is.EqualTo("[]"));
    }

    [Test]
    public void BuildSpecialTokens_IndexedViewSchema_EscapesSingleQuotes()
    {
        var template = new Template { Name = "Core", IndexedViewSchema = "[{\"Name\":\"test's view\"}]" };

        var tokens = ProductQuench.BuildSpecialTokens(template);

        Assert.That(tokens["IndexedViewSchema_Core"], Is.EqualTo("[{\"Name\":\"test''s view\"}]"));
    }

    #endregion
}

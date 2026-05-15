// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;
using Schema.Isolators;

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

    #region Product Script Server Routing Tests

    [Test]
    public void QuenchScriptsToServerWithCheckpoint_UsesRequestedServerForCommand()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            var schemaPackagePath = "Product";
            var productPath = Path.Combine(schemaPackagePath, "Product.json");
            var file = Substitute.For<IFile>();
            var directory = Substitute.For<IDirectory>();

            file.Exists(schemaPackagePath).Returns(false);
            directory.Exists(schemaPackagePath).Returns(true);
            file.Exists(productPath).Returns(true);
            file.ReadAllText(productPath).Returns("""
                                                  {
                                                    "Name": "TestProduct",
                                                    "Platform": "SqlServer",
                                                    "ScriptFolders": []
                                                  }
                                                  """);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["SchemaPackagePath"] = schemaPackagePath,
                    ["Target:Server"] = "primary-server",
                    ["Target:SecondaryServers"] = "secondary-server",
                    ["WhatIfONLY"] = "true"
                })
                .Build();

            FactoryContainer.Register<IConfigurationRoot>(config);
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(directory);

            try
            {
                var quench = new RecordingProductQuench();

                InvokeQuenchScriptsToServerWithCheckpoint(quench, "secondary-server");

                Assert.That(quench.CommandServers, Is.EqualTo(new[] { "secondary-server" }));
            }
            finally
            {
                FactoryContainer.Clear();
            }
        }
    }

    private static void InvokeQuenchScriptsToServerWithCheckpoint(ProductQuench quench, string server)
    {
        var method = typeof(ProductQuench).GetMethod("QuenchScriptsToServerWithCheckpoint",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method!.Invoke(quench, [server, "Before Product", Array.Empty<SqlScript>(), true]);
    }

    private sealed class RecordingProductQuench : ProductQuench
    {
        public List<string> CommandServers { get; } = [];

        internal override IDbCommand GetCommand(string server)
        {
            CommandServers.Add(server);
            var command = Substitute.For<IDbCommand>();
            command.Connection.Returns(Substitute.For<IDbConnection>());
            return command;
        }
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

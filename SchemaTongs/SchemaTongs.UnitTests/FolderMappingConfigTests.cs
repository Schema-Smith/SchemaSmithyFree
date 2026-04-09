// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Schema.Domain;

namespace SchemaTongs.UnitTests;

[TestFixture]
public class FolderMappingConfigTests
{
    private static IConfigurationRoot BuildConfig(Dictionary<string, string> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IConfigurationRoot EmptyConfig()
        => BuildConfig(new Dictionary<string, string>());

    [Test]
    public void DefaultMapping_SqlServer_ViewsReturnsViews()
    {
        var config = new FolderMappingConfig(EmptyConfig(), Platform.SqlServer);
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("Views"));
    }

    [Test]
    public void DefaultMapping_PostgreSQL_ViewsReturnsViews()
    {
        var config = new FolderMappingConfig(EmptyConfig(), Platform.PostgreSQL);
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("Views"));
    }

    [Test]
    public void DefaultMapping_MySQL_ViewsReturnsViews()
    {
        var config = new FolderMappingConfig(EmptyConfig(), Platform.MySQL);
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("Views"));
    }

    [Test]
    public void CustomMapping_OverridesDefault()
    {
        var configValues = new Dictionary<string, string> { ["FolderMapping:Views"] = "DatabaseViews" };
        var config = new FolderMappingConfig(BuildConfig(configValues), Platform.SqlServer);
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("DatabaseViews"));
    }

    [Test]
    public void UnmappedType_UsesDefault_WhenOtherTypeMapped()
    {
        var configValues = new Dictionary<string, string> { ["FolderMapping:Views"] = "DatabaseViews" };
        var config = new FolderMappingConfig(BuildConfig(configValues), Platform.SqlServer);
        Assert.That(config.GetFolderName(ScriptObjectType.Functions), Is.EqualTo("Functions"));
    }

    [Test]
    public void UnknownTypeName_ThrowsArgumentException()
    {
        var configValues = new Dictionary<string, string> { ["FolderMapping:Bogus"] = "Foo" };
        Assert.Throws<ArgumentException>(() =>
            new FolderMappingConfig(BuildConfig(configValues), Platform.SqlServer));
    }

    [Test]
    public void FixedFolderType_IndexedViews_ThrowsArgumentException()
    {
        var configValues = new Dictionary<string, string> { ["FolderMapping:IndexedViews"] = "CustomIV" };
        Assert.Throws<ArgumentException>(() =>
            new FolderMappingConfig(BuildConfig(configValues), Platform.SqlServer));
    }

    [Test]
    public void FixedFolderType_MaterializedViews_ThrowsArgumentException()
    {
        var configValues = new Dictionary<string, string> { ["FolderMapping:MaterializedViews"] = "CustomMV" };
        Assert.Throws<ArgumentException>(() =>
            new FolderMappingConfig(BuildConfig(configValues), Platform.PostgreSQL));
    }

    [Test]
    public void FixedFolderType_None_ThrowsArgumentException()
    {
        var configValues = new Dictionary<string, string> { ["FolderMapping:None"] = "CustomNone" };
        Assert.Throws<ArgumentException>(() =>
            new FolderMappingConfig(BuildConfig(configValues), Platform.SqlServer));
    }

    [Test]
    public void Validate_DuplicateFolderNames_ThrowsArgumentException()
    {
        var configValues = new Dictionary<string, string>
        {
            ["FolderMapping:Views"] = "Shared",
            ["FolderMapping:Functions"] = "Shared"
        };
        var config = new FolderMappingConfig(BuildConfig(configValues), Platform.SqlServer);
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void EmptyConfigValue_KeepsDefault()
    {
        var configValues = new Dictionary<string, string> { ["FolderMapping:Views"] = "" };
        var config = new FolderMappingConfig(BuildConfig(configValues), Platform.SqlServer);
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("Views"));
    }

    [Test]
    public void SqlServerDefaults_AllTypesReturnCorrectNames()
    {
        var config = new FolderMappingConfig(EmptyConfig(), Platform.SqlServer);

        Assert.That(config.GetFolderName(ScriptObjectType.Schemas), Is.EqualTo("Schemas"));
        Assert.That(config.GetFolderName(ScriptObjectType.DataTypes), Is.EqualTo("DataTypes"));
        Assert.That(config.GetFolderName(ScriptObjectType.FullTextCatalogs), Is.EqualTo("FullTextCatalogs"));
        Assert.That(config.GetFolderName(ScriptObjectType.FullTextStopLists), Is.EqualTo("FullTextStopLists"));
        Assert.That(config.GetFolderName(ScriptObjectType.XMLSchemaCollections), Is.EqualTo("XMLSchemaCollections"));
        Assert.That(config.GetFolderName(ScriptObjectType.Functions), Is.EqualTo("Functions"));
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("Views"));
        Assert.That(config.GetFolderName(ScriptObjectType.Procedures), Is.EqualTo("Procedures"));
        Assert.That(config.GetFolderName(ScriptObjectType.Triggers), Is.EqualTo("Triggers"));
        Assert.That(config.GetFolderName(ScriptObjectType.DDLTriggers), Is.EqualTo("DDLTriggers"));
    }

    [Test]
    public void PostgreSqlDefaults_AllTypesReturnCorrectNames()
    {
        var config = new FolderMappingConfig(EmptyConfig(), Platform.PostgreSQL);

        Assert.That(config.GetFolderName(ScriptObjectType.Schemas), Is.EqualTo("Schemas"));
        Assert.That(config.GetFolderName(ScriptObjectType.DomainTypes), Is.EqualTo("Domain Types"));
        Assert.That(config.GetFolderName(ScriptObjectType.EnumTypes), Is.EqualTo("Enum Types"));
        Assert.That(config.GetFolderName(ScriptObjectType.CompositeTypes), Is.EqualTo("Composite Types"));
        Assert.That(config.GetFolderName(ScriptObjectType.Functions), Is.EqualTo("Functions"));
        Assert.That(config.GetFolderName(ScriptObjectType.TriggerFunctions), Is.EqualTo("Trigger Functions"));
        Assert.That(config.GetFolderName(ScriptObjectType.WindowFunctions), Is.EqualTo("Window Functions"));
        Assert.That(config.GetFolderName(ScriptObjectType.Aggregates), Is.EqualTo("Aggregates"));
        Assert.That(config.GetFolderName(ScriptObjectType.Procedures), Is.EqualTo("Procedures"));
        Assert.That(config.GetFolderName(ScriptObjectType.Sequences), Is.EqualTo("Sequences"));
        Assert.That(config.GetFolderName(ScriptObjectType.Rules), Is.EqualTo("Rules"));
        Assert.That(config.GetFolderName(ScriptObjectType.Triggers), Is.EqualTo("Triggers"));
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("Views"));
    }

    [Test]
    public void MySqlDefaults_AllTypesReturnCorrectNames()
    {
        var config = new FolderMappingConfig(EmptyConfig(), Platform.MySQL);

        Assert.That(config.GetFolderName(ScriptObjectType.Events), Is.EqualTo("Events"));
        Assert.That(config.GetFolderName(ScriptObjectType.Functions), Is.EqualTo("Functions"));
        Assert.That(config.GetFolderName(ScriptObjectType.Procedures), Is.EqualTo("Procedures"));
        Assert.That(config.GetFolderName(ScriptObjectType.Triggers), Is.EqualTo("Triggers"));
        Assert.That(config.GetFolderName(ScriptObjectType.Views), Is.EqualTo("Views"));
    }

    [Test]
    public void TypeNotInPlatformDefaults_ReturnsNull()
    {
        // MySQL has no Schemas type
        var config = new FolderMappingConfig(EmptyConfig(), Platform.MySQL);
        Assert.That(config.GetFolderName(ScriptObjectType.Schemas), Is.Null);
    }

    [Test]
    public void Validate_UniqueNames_DoesNotThrow()
    {
        var config = new FolderMappingConfig(EmptyConfig(), Platform.SqlServer);
        Assert.DoesNotThrow(() => config.Validate());
    }
}

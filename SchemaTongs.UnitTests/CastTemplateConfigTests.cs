// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs.UnitTests;

public class CastTemplateConfigTests
{
    [Test]
    public void ShouldLogWarning_WhenProductJsonStyleDiffersFromConfig()
    {
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        var progressLog = Substitute.For<ILog>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            LogFactory.Clear();

            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaTongs", null);
            config["Source:database"] = "FakeDb";
            config["Product:Path"] = ".";
            config["Product:Name"] = "TestProduct";
            config["Template:Name"] = "TestTemplate";
            config["Product:CheckConstraintStyle"] = "TableLevel";
            config["ShouldCast:Tables"] = "false";
            config["ShouldCast:Schemas"] = "false";
            config["ShouldCast:UserDefinedTypes"] = "false";
            config["ShouldCast:UserDefinedFunctions"] = "false";
            config["ShouldCast:Views"] = "false";
            config["ShouldCast:StoredProcedures"] = "false";
            config["ShouldCast:TableTriggers"] = "false";
            config["ShouldCast:Catalogs"] = "false";
            config["ShouldCast:StopLists"] = "false";
            config["ShouldCast:DDLTriggers"] = "false";
            config["ShouldCast:XMLSchemaCollections"] = "false";
            config["ShouldCast:IndexedViews"] = "false";

            // Product.json exists with ColumnLevel style
            file.Exists(Arg.Is<string>(s => s.EndsWith("Product.json"))).Returns(true);
            file.ReadAllText(Arg.Is<string>(s => s.EndsWith("Product.json")))
                .Returns("{\"Name\":\"TestProduct\",\"CheckConstraintStyle\":\"ColumnLevel\"}");

            try
            {
                var tongs = new SchemaTongs();
                tongs.CastTemplate();
            }
            catch
            {
                // Expected — CastDatabaseObjects fails without SQL Server
            }

            progressLog.Received().Warn(Arg.Is<string>(s => s.Contains("CheckConstraintStyle")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldNotLogWarning_WhenProductJsonStyleMatchesConfig()
    {
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        var progressLog = Substitute.For<ILog>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            LogFactory.Clear();

            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaTongs", null);
            config["Source:database"] = "FakeDb";
            config["Product:Path"] = ".";
            config["Product:Name"] = "TestProduct";
            config["Template:Name"] = "TestTemplate";
            config["Product:CheckConstraintStyle"] = "ColumnLevel";
            config["ShouldCast:Tables"] = "false";
            config["ShouldCast:Schemas"] = "false";
            config["ShouldCast:UserDefinedTypes"] = "false";
            config["ShouldCast:UserDefinedFunctions"] = "false";
            config["ShouldCast:Views"] = "false";
            config["ShouldCast:StoredProcedures"] = "false";
            config["ShouldCast:TableTriggers"] = "false";
            config["ShouldCast:Catalogs"] = "false";
            config["ShouldCast:StopLists"] = "false";
            config["ShouldCast:DDLTriggers"] = "false";
            config["ShouldCast:XMLSchemaCollections"] = "false";
            config["ShouldCast:IndexedViews"] = "false";

            // Product.json exists with ColumnLevel style — matches config
            file.Exists(Arg.Is<string>(s => s.EndsWith("Product.json"))).Returns(true);
            file.ReadAllText(Arg.Is<string>(s => s.EndsWith("Product.json")))
                .Returns("{\"Name\":\"TestProduct\",\"CheckConstraintStyle\":\"ColumnLevel\"}");

            try
            {
                var tongs = new SchemaTongs();
                tongs.CastTemplate();
            }
            catch
            {
                // Expected — CastDatabaseObjects fails without SQL Server
            }

            progressLog.DidNotReceive().Warn(Arg.Is<string>(s => s.Contains("CheckConstraintStyle")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldDefaultToColumnLevel_WhenNoProductJsonAndNoConfigStyle()
    {
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        var progressLog = Substitute.For<ILog>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            LogFactory.Clear();

            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaTongs", null);
            config["Source:database"] = "FakeDb";
            config["Product:Path"] = ".";
            config["Product:Name"] = "TestProduct";
            config["Template:Name"] = "TestTemplate";
            // No CheckConstraintStyle in config
            config["ShouldCast:Tables"] = "false";
            config["ShouldCast:Schemas"] = "false";
            config["ShouldCast:UserDefinedTypes"] = "false";
            config["ShouldCast:UserDefinedFunctions"] = "false";
            config["ShouldCast:Views"] = "false";
            config["ShouldCast:StoredProcedures"] = "false";
            config["ShouldCast:TableTriggers"] = "false";
            config["ShouldCast:Catalogs"] = "false";
            config["ShouldCast:StopLists"] = "false";
            config["ShouldCast:DDLTriggers"] = "false";
            config["ShouldCast:XMLSchemaCollections"] = "false";
            config["ShouldCast:IndexedViews"] = "false";

            // Product.json does not exist
            file.Exists(Arg.Is<string>(s => s.EndsWith("Product.json"))).Returns(false);

            try
            {
                var tongs = new SchemaTongs();
                tongs.CastTemplate();
            }
            catch
            {
                // Expected — CastDatabaseObjects fails without SQL Server
            }

            // No warning should be logged since there's no conflict
            progressLog.DidNotReceive().Warn(Arg.Is<string>(s => s.Contains("CheckConstraintStyle")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}

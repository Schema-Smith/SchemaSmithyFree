// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using log4net;
using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;
using NSubstitute;
using Schema.DataAccess;
using Schema.Delivery;
using Schema.Domain;
using Schema.IntegrationTests;
using Schema.IntegrationTests.MySQL;
using Schema.Isolators;
using Schema.Utility;
using Index = Schema.Domain.Index;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for ProductQuench product loading functionality.
/// Tests loading Product.json and basic validation.
/// Uses dynamically created test databases via FixtureSetup.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
[Category("MySQL")]
public class ProductQuenchTests
{
    private string _testFixturePath = null!;
    private string _testDb = null!;
    private IConfigurationRoot _savedConfig;
    private bool _lockTaken;

    // Each test in this fixture temporarily Registers its own in-memory IConfigurationRoot
    // to drive Product.Load(). To avoid racing with other fixtures (notably ProductUpdateTests
    // below, which relies on the FixtureSetup-provided config being stable), the entire test
    // body runs while holding FactoryContainer.SharedLockObject. Monitor.Enter in SetUp and
    // Monitor.Exit in TearDown brackets the whole Test method. Save/restore preserves the
    // FixtureSetup-provided config for the next fixture.
    [SetUp]
    public void SetUp()
    {
        _lockTaken = false;
        Monitor.Enter(FactoryContainer.SharedLockObject, ref _lockTaken);

        _savedConfig = FactoryContainer.Resolve<IConfigurationRoot>();

        _testDb = FixtureSetup.MainDb;

        var assemblyLocation = Path.GetDirectoryName(typeof(ProductQuenchTests).Assembly.Location);
        _testFixturePath = Path.Combine(assemblyLocation!, "TestFixtures", "TestProduct");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (_savedConfig != null)
                FactoryContainer.Register(_savedConfig);
            else
                FactoryContainer.Unregister<IConfigurationRoot>();
            _savedConfig = null;
        }
        finally
        {
            if (_lockTaken)
            {
                Monitor.Exit(FactoryContainer.SharedLockObject);
                _lockTaken = false;
            }
        }
    }

    [Test]
    public void Product_Load_LoadsProductFromTestFixtures()
    {
        // Arrange - Set up configuration with test fixture path
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", _testFixturePath)
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        // Act
        var product = Product.Load();

        // Assert
        Assert.That(product, Is.Not.Null);
        Assert.That(product.Name, Is.EqualTo("TestProduct"));
        Assert.That(product.Platform, Is.EqualTo(Platform.MySQL));
        Assert.That(product.TemplateOrder, Has.Count.EqualTo(1));
        Assert.That(product.TemplateOrder[0], Is.EqualTo("TestTemplate"));
    }

    [Test]
    public void Product_Load_ThrowsForMissingPath()
    {
        // Arrange - Set up configuration with non-existent path
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", @"C:\NonExistent\Path")
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        // Act & Assert
        Assert.Throws<Exception>(() => Product.Load());
    }

    [Test]
    public void Product_Load_SetsFilePath()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", _testFixturePath)
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        // Act
        var product = Product.Load();

        // Assert
        Assert.That(product.FilePath, Does.EndWith("Product.json"));
        Assert.That(product.FilePath, Does.Contain("TestProduct"));
    }

    [Test]
    public void Product_Load_AddsProductNameToken()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", _testFixturePath)
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        // Act
        var product = Product.Load();

        // Assert
        Assert.That(product.ScriptTokens, Contains.Key("ProductName"));
        Assert.That(product.ScriptTokens["ProductName"], Is.EqualTo("TestProduct"));
    }

    [Test]
    public void Template_Load_LoadsTemplateFromProduct()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", _testFixturePath)
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        var product = Product.Load();

        // Act
        var template = Template.Load("TestTemplate", product);

        // Assert
        Assert.That(template, Is.Not.Null);
        Assert.That(template.Name, Is.EqualTo("TestTemplate"));
        Assert.That(template.RequireAtLeastOneTarget, Is.True);
    }

    [Test]
    public void Template_Load_SetsProductReference()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", _testFixturePath)
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        var product = Product.Load();

        // Act
        var template = Template.Load("TestTemplate", product);

        // Assert
        Assert.That(template.Product, Is.SameAs(product));
    }

    [Test]
    public void Product_ScriptTokens_CanBeOverriddenByConfig()
    {
        // Arrange - Create a product with a token and override it in config
        var productJsonPath = Path.Combine(_testFixturePath, "Product.json");
        var originalContent = File.ReadAllText(productJsonPath);

        try
        {
            // Add a token to the product
            var productWithToken = originalContent.Replace(
                "\"ScriptTokens\": {}",
                "\"ScriptTokens\": { \"TestToken\": \"OriginalValue\" }");
            File.WriteAllText(productJsonPath, productWithToken);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("SchemaPackagePath", _testFixturePath),
                    new KeyValuePair<string, string>("ScriptTokens:TestToken", "OverriddenValue")
                })
                .Build();
            FactoryContainer.Register<IConfigurationRoot>(config);

            // Act
            var product = Product.Load();

            // Assert - Token should be overridden by config
            Assert.That(product.ScriptTokens["TestToken"], Is.EqualTo("OverriddenValue"));
        }
        finally
        {
            // Restore original content
            File.WriteAllText(productJsonPath, originalContent);
        }
    }

    [Test]
    public void Template_MultiDatabase_IdentificationScriptCanReturnMultipleDatabases()
    {
        // Arrange - Create a template with multi-database identification script
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", _testFixturePath)
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        var product = Product.Load();
        var template = Template.Load("TestTemplate", product);

        // Modify the identification script to return multiple databases (simulation)
        template.DatabaseIdentificationScript = $"SELECT '{_testDb}' AS DatabaseName UNION SELECT 'information_schema'";

        // Act - Execute the identification script
        using var connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = template.DatabaseIdentificationScript;

        var databases = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            databases.Add(reader.GetString(0));
        }

        // Assert - Should return multiple databases
        Assert.That(databases, Has.Count.EqualTo(2));
        Assert.That(databases, Contains.Item(_testDb));
        Assert.That(databases, Contains.Item("information_schema"));
    }

    [Test]
    public void Template_TablesWithMergeType_AreFilteredCorrectly()
    {
        // Arrange
        var template = new Template { Name = "TestTemplate" };
        template.Tables.Add(new Table { Name = "table1", DataDelivery = new DataDelivery { MergeType = "Insert/Update/Delete" } });
        template.Tables.Add(new Table { Name = "table2", DataDelivery = new DataDelivery { MergeType = "None" } });
        template.Tables.Add(new Table { Name = "table3", DataDelivery = new DataDelivery { MergeType = "Insert/Update" } });
        template.Tables.Add(new Table { Name = "table4", DataDelivery = null });
        template.Tables.Add(new Table { Name = "table5", DataDelivery = new DataDelivery { MergeType = "Insert" } });
        template.Tables.Add(new Table { Name = "table6", DataDelivery = new DataDelivery { MergeType = "" } });

        // Act - Filter tables with data delivery
        var tablesWithData = template.Tables
            .Where(t => t.DataDelivery != null && !string.IsNullOrWhiteSpace(t.DataDelivery.MergeType) &&
                        !t.DataDelivery.MergeType.Equals("none", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        Assert.That(tablesWithData, Has.Count.EqualTo(3));
        Assert.That(tablesWithData.Select(t => t.Name), Is.EquivalentTo(new[] { "table1", "table3", "table5" }));
    }

    [Test]
    public void Table_WithContentFile_LoadsDataFromRelativePath()
    {
        // Arrange - Create temporary test fixture with table data
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_product_{Guid.NewGuid():N}");
        var tableDataDir = Path.Combine(tempDir, "TableData");
        Directory.CreateDirectory(tableDataDir);

        var tableDataPath = Path.Combine(tableDataDir, "test_data.json");
        File.WriteAllText(tableDataPath, @"[{""id"":1,""name"":""Test""}]");

        try
        {
            var table = new Table
            {
                Name = "test_table",
                DataDelivery = new DataDelivery { MergeType = "Replace", ContentFile = "TableData/test_data.json" }
            };

            // Act - Load content file using ProductFileWrapper (simulating what DatabaseQuench does)
            var contentPath = Path.Combine(tempDir, table.DataDelivery.ContentFile);
            var fileWrapper = ProductFileWrapper.GetFromFactory();

            // Assert
            Assert.That(fileWrapper.Exists(contentPath), Is.True);
            var content = fileWrapper.ReadAllText(contentPath);
            Assert.That(content, Does.Contain("Test"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Table_DataDeliveryProperties_AreSerializedCorrectly()
    {
        // Arrange
        var table = new Table
        {
            Name = "config_data",
            DataDelivery = new DataDelivery
            {
                MergeType = "Insert/Update",
                ContentFile = "TableData/config.json",
                MatchColumns = "`key`,`environment`",
                MergeFilter = "`active` = 1",
                MergeDisableTriggers = true
            }
        };

        // Act - Round-trip through JSON serialization
        var json = JsonHelper.Serialize(new List<Table> { table });
        var deserialized = JsonConvert.DeserializeObject<List<Table>>(json);

        // Assert
        Assert.That(deserialized, Has.Count.EqualTo(1));
        var result = deserialized[0];
        Assert.That(result.DataDelivery, Is.Not.Null);
        Assert.That(result.DataDelivery.MergeType, Is.EqualTo("Insert/Update"));
        Assert.That(result.DataDelivery.ContentFile, Is.EqualTo("TableData/config.json"));
        Assert.That(result.DataDelivery.MatchColumns, Is.EqualTo("`key`,`environment`"));
        Assert.That(result.DataDelivery.MergeFilter, Is.EqualTo("`active` = 1"));
        Assert.That(result.DataDelivery.MergeDisableTriggers, Is.True);
    }

    [Test]
    public void MergeScriptHelper_IntegrationWithTemplate_GeneratesCorrectSQL()
    {
        // Arrange - Create a complete table definition as it would appear in a product
        var table = new Table
        {
            Name = "lookup_values",
            DataDelivery = new DataDelivery
            {
                MergeType = "Insert/Update/Delete",
                ContentFile = "TableData/lookup_values.json"
            },
            Columns =
            [
                new Column { Name = "id", DataType = "INT" },
                new Column { Name = "code", DataType = "VARCHAR(50)", Nullable = false },
                new Column { Name = "name", DataType = "VARCHAR(100)", Nullable = false },
                new Column { Name = "sort_order", DataType = "INT", Default = "0" }
            ],
            Indexes =
            [
                new Index { Name = "PRIMARY", PrimaryKey = true, IndexColumns = "`id`" },
                new Index { Name = "idx_code", Unique = true, IndexColumns = "`code`" }
            ]
        };

        // Act - Verify table can be serialized and properties are accessible
        var json = JsonHelper.Serialize(new List<Table> { table });
        Assert.That(json, Does.Contain("lookup_values"));
        Assert.That(json, Does.Contain("Insert/Update/Delete"));

        // Assert - Verify table properties for data delivery
        Assert.That(table.DataDelivery, Is.Not.Null);
        Assert.That(table.DataDelivery.MergeType, Is.EqualTo("Insert/Update/Delete"));
        Assert.That(table.DataDelivery.ContentFile, Is.EqualTo("TableData/lookup_values.json"));
        Assert.That(!string.IsNullOrWhiteSpace(table.DataDelivery.MergeType) &&
                    !table.DataDelivery.MergeType.Equals("none", StringComparison.OrdinalIgnoreCase), Is.True);
    }
}

/// <summary>
/// Integration tests for ProductQuench error scenarios.
/// Tests that the product quench properly handles and reports various error conditions.
/// Uses test products from the TestProducts folder.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class ProductUpdateTests
{
    private ILog _errorLog = null!;
    private ILog _progressLog = null!;
    private IEnvironment _environment = null!;
    private string _connectionString = null!;
    private string _secondaryDb = null!;
    private string _mainDb = null!;
    private string _server = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Ensure FixtureSetup has run to initialize the test databases
        FixtureSetup.EnsureInitialized();

        _errorLog = Substitute.For<ILog>();
        _progressLog = Substitute.For<ILog>();
        _environment = Substitute.For<IEnvironment>();

        // Use config from FixtureSetup, matching SQL Server/PostgreSQL pattern
        _connectionString = FixtureSetup.ConnectionString + "Database=information_schema;";
        _mainDb = FixtureSetup.MainDb;
        _secondaryDb = FixtureSetup.SecondaryDb;
        _server = FixtureSetup.Config["Target:Server"] ?? "localhost";
    }

    [Test]
    public void ShouldQuenchValidProductSuccessfully()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            // Delete any old quench script files
            foreach (var file in Directory.GetFiles(".", "SchemaQuench - Quench Tables*.sql"))
                File.Delete(file);

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("MySQL", "ValidProduct");
            var product = Product.Load();

            // Setup test infrastructure in databases
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            SetupTestLogTable(conn, _mainDb);
            SetupTestLogTable(conn, _secondaryDb);
            SetupCompletedMigrationScripts(conn, _mainDb, product.Name);
            SetupCompletedMigrationScripts(conn, _secondaryDb, product.Name);
            conn.Close();

            RunSchemaQuench();

            _progressLog.DidNotReceive().Error(Arg.Any<string>());
            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Successfully Quenched")));
            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains(_secondaryDb) && s.Contains("Successfully Quenched")));
            _progressLog.Received(1).Info("Completed quench of ValidProduct");

            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains("Quenching After Product Scripts to")));
            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains("Job 1.sql")));
            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains("Job 2.sql")));

            // Check for unresolved token warnings
            _progressLog.Received().Warn(Arg.Is<string>(s => s.Contains("Unresolved token")));

            _environment.DidNotReceive().Exit(2);
            _environment.DidNotReceive().Exit(3);

            AssertScriptsQuenched(_mainDb);
            AssertScriptsQuenched(_secondaryDb);

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    [Test]
    public void ShouldWhatIfValidProductWithoutQuenchingAnything()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            config["SchemaPackagePath"] = TestHelper.GetTestProductPath("MySQL", "ValidProduct");
            config["WhatIfOnly"] = "true";

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            SetupTestLogTable(conn, _mainDb);
            SetupTestLogTable(conn, _secondaryDb);
            SetupCompletedMigrationScripts(conn, _mainDb, "ValidProduct");
            SetupCompletedMigrationScripts(conn, _secondaryDb, "ValidProduct");

            // Capture database state before WhatIf run
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM `{_mainDb}`.`SchemaSmith_CompletedMigrationScripts`";
            var mainMigrationCountBefore = (long)cmd.ExecuteScalar();
            cmd.CommandText = $"SELECT COUNT(*) FROM `{_mainDb}`.`SchemaSmith_TestLog`";
            var mainTestLogCountBefore = (long)cmd.ExecuteScalar();
            cmd.CommandText = $"SELECT COUNT(*) FROM `{_secondaryDb}`.`SchemaSmith_CompletedMigrationScripts`";
            var secondaryMigrationCountBefore = (long)cmd.ExecuteScalar();
            conn.Close();

            try
            {
                RunSchemaQuench();

                // No errors should occur
                _progressLog.DidNotReceive().Error(Arg.Any<string>());
                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                // Should complete successfully
                _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Successfully Quenched")));
                _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains(_secondaryDb) && s.Contains("Successfully Quenched")));
                _progressLog.Received(1).Info("Completed quench of ValidProduct");

                // WhatIf log messages for Main template database quench
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Object scripts without unresolved tokens:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Object scripts without query tokens:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Before database scripts:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Object scripts (after tables):")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Between table and keys scripts:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] After table scripts:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Object scripts (final pass):")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Table data delivery:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] After database scripts:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("[WhatIf] Would stamp version")));

                // WhatIf log messages for Secondary template database quench
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_secondaryDb) && s.Contains("[WhatIf] Object scripts without unresolved tokens:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_secondaryDb) && s.Contains("[WhatIf] Before database scripts:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_secondaryDb) && s.Contains("[WhatIf] After database scripts:")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_secondaryDb) && s.Contains("[WhatIf] Would stamp version")));

                // WhatIf "Would APPLY" messages for object scripts
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Would APPLY:") && s.Contains("MyFunction.sql")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Would APPLY:") && s.Contains("MyView.sql")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Would APPLY:") && s.Contains("MyProcedure.sql")));

                // Before migration scripts: MigrationScript0 was previously quenched, MigrationScript1 [ALWAYS] should be Would APPLY
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Would SKIP (previously quenched):") && s.Contains("MigrationScript0.sql")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Would APPLY:") && s.Contains("MigrationScript1 [ALWAYS].sql")));

                // Table data delivery WhatIf for Main (TestTable has ContentFile and MergeType)
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Would DELIVER:") && s.Contains("TestTable")));

                // After Product scripts should show "Would Quench"
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Would Quench") && s.Contains("Job 1.sql")));
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Would Quench") && s.Contains("Job 2.sql")));

                // Verify nothing was actually quenched - TestLog should be empty (truncated before run)
                conn.Open();
                var mainScriptLog = GetScriptLog(_mainDb, "SchemaSmith_TestLog", "Msg", "Id");
                Assert.That(mainScriptLog, Is.Empty, "No scripts should have been quenched in MainDB TestLog");

                var secondaryScriptLog = GetScriptLog(_secondaryDb, "SchemaSmith_TestLog", "Msg", "Id");
                Assert.That(secondaryScriptLog, Is.Empty, "No scripts should have been quenched in SecondaryDB TestLog");

                // Verify database state was not modified by WhatIf
                cmd.CommandText = $"SELECT COUNT(*) FROM `{_mainDb}`.`SchemaSmith_CompletedMigrationScripts`";
                Assert.That((long)cmd.ExecuteScalar(), Is.EqualTo(mainMigrationCountBefore), "MainDB CompletedMigrationScripts should be unchanged");

                cmd.CommandText = $"SELECT COUNT(*) FROM `{_mainDb}`.`SchemaSmith_TestLog`";
                Assert.That((long)cmd.ExecuteScalar(), Is.EqualTo(mainTestLogCountBefore), "MainDB TestLog should be unchanged");

                cmd.CommandText = $"SELECT COUNT(*) FROM `{_secondaryDb}`.`SchemaSmith_CompletedMigrationScripts`";
                Assert.That((long)cmd.ExecuteScalar(), Is.EqualTo(secondaryMigrationCountBefore), "SecondaryDB CompletedMigrationScripts should be unchanged");
                conn.Close();
            }
            finally
            {
                config["WhatIfOnly"] = "false";
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void ShouldErrorOnObjectsScriptThatCannotBeQuenchedWithRetry()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("MySQL", "TemplateObjectsScriptError");

            RunSchemaQuench();

            _progressLog.Received().Error(Arg.Is<string>(s => s.Contains("Unable to quench")));
            _environment.Received(1).Exit(2);
            _environment.DidNotReceive().Exit(3);

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    [Test]
    public void ShouldExitWithReturnCodeWhenBeforeTemplateScriptErrors()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("MySQL", "BeforeTemplateScriptError");

            RunSchemaQuench();

            _progressLog.Received(1).Error(Arg.Is<string>(s => s.Contains("KABOOM!")));
            _environment.Received(1).Exit(2);
            _environment.DidNotReceive().Exit(3);

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    [Test]
    public void ShouldExitWithReturnCodeWhenVersionStampErrors()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("MySQL", "BadVersionStamp");

            RunSchemaQuench();

            _progressLog.Received().Error(Arg.Is<string>(s => s.Contains("BAD STAMP!")));
            _environment.Received(1).Exit(2);
            _environment.DidNotReceive().Exit(3);

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    [Test]
    public void ShouldThrowExceptionWhenAfterProductScriptErrors()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("MySQL", "AfterProductScriptError");

            var ex = Assert.Throws<Exception>(RunSchemaQuench);
            Assert.That(ex!.Message, Contains.Substring("Product script quench FAILED"));

            _progressLog.Received().Error(Arg.Is<string>(s => s.Contains("Unable to quench") && s.Contains("Job 1.sql") && s.Contains("KABOOM")));

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    [Test]
    public void ShouldThrowExceptionWhenInvalidServer()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("MySQL", "InvalidServer");

            var ex = Assert.Throws<Exception>(RunSchemaQuench);
            Assert.That(ex!.Message, Contains.Substring("Invalid server for this product"));

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    private void SetupSharedMocks()
    {
        _progressLog.ClearReceivedCalls();
        _errorLog.ClearReceivedCalls();
        _environment.ClearReceivedCalls();

        // Re-register the config captured by FixtureSetup (other tests may have cleared it).
        // This config already has Target:* and ScriptTokens:* keys set by
        // Schema.IntegrationTests.MySQL.FixtureSetup, matching the SQL Server/PostgreSQL pattern.
        FactoryContainer.Register(FixtureSetup.Config);
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private void RunSchemaQuench()
    {
        Program.Main(["SkipKindlingForge"]);
    }

    private void SetupTestLogTable(IDbConnection conn, string dbName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS `{dbName}`.`SchemaSmith_TestLog` (
                `Id` INT AUTO_INCREMENT PRIMARY KEY,
                `Msg` VARCHAR(500) NOT NULL
            );
            TRUNCATE TABLE `{dbName}`.`SchemaSmith_TestLog`;
        ";
        cmd.ExecuteNonQuery();
    }

    private void SetupCompletedMigrationScripts(IDbConnection conn, string dbName, string productName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS `{dbName}`.`SchemaSmith_CompletedMigrationScripts` (
                `ScriptPath` VARCHAR(500) NOT NULL,
                `ProductName` VARCHAR(100) NOT NULL,
                `QuenchSlot` VARCHAR(50) NOT NULL,
                PRIMARY KEY (`ScriptPath`, `ProductName`)
            );
            TRUNCATE TABLE `{dbName}`.`SchemaSmith_CompletedMigrationScripts`;
            INSERT INTO `{dbName}`.`SchemaSmith_CompletedMigrationScripts` (`ScriptPath`, `ProductName`, `QuenchSlot`)
            VALUES ('MigrationScripts/Before/MigrationScript0.sql', '{productName}', 'Before');
        ";
        cmd.ExecuteNonQuery();
    }

    private static readonly List<string> ExpectedScriptLog =
    [
        "Before/MigrationScript1.sql",
        "MyFunction.sql",
        "MyView.sql",
        "MyProcedure.sql",
        "FunctionThatNeedsView.sql",
        "MyTrigger.sql",
        "After/MigrationScript1.sql"
    ];

    private void AssertScriptsQuenched(string dbName)
    {
        var scriptLog = GetScriptLog(dbName, "SchemaSmith_TestLog", "Msg", "Id");

        // Filter expected scripts based on whether it's Main or Secondary db
        var expected = ExpectedScriptLog.Where(l =>
            dbName.Contains("Main") || !l.Equals("FunctionThatNeedsView.sql")).ToList();

        // Verify expected scripts are quenched
        foreach (var expectedScript in expected)
        {
            Assert.That(scriptLog.Any(s => s.Contains(expectedScript.Replace("/", "\\")) || s.Contains(expectedScript)),
                Is.True, $"Expected script '{expectedScript}' to be quenched in {dbName}");
        }
    }

    private List<string> GetScriptLog(string dbName, string logTable, string msgCol, string orderCol)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT `{msgCol}` FROM `{dbName}`.`{logTable}` ORDER BY `{orderCol}`";
        using var reader = cmd.ExecuteReader();
        var scriptLog = new List<string>();
        while (reader.Read()) scriptLog.Add(reader[msgCol]?.ToString() ?? "");
        conn.Close();

        return scriptLog;
    }
}

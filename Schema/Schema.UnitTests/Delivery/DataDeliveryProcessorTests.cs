// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using NSubstitute;
using Schema.Delivery;

namespace Schema.UnitTests.Delivery;

[TestFixture]
public class DataDeliveryProcessorTests
{
    private class TestColumn : IDeliverableColumn
    {
        public string Name { get; set; }
        public bool Nullable { get; set; }
    }

    private class TestFK : IDeliverableForeignKey
    {
        public string Columns { get; set; }
        public string RelatedTable { get; set; }
        public string RelatedTableSchema { get; set; }
    }

    private class TestTable : IDeliverableTable
    {
        public string Name { get; set; }
        public string Schema { get; set; }
        public DataDelivery DataDelivery { get; set; }
        public IReadOnlyList<IDeliverableColumn> DeliverableColumns { get; set; } = new List<IDeliverableColumn>();
        public IReadOnlyList<IDeliverableForeignKey> DeliverableForeignKeys { get; set; } = new List<IDeliverableForeignKey>();
    }

    private IMergeScriptHelper _mockHelper;
    private IDbCommand _mockCommand;
    private List<string> _executedScripts;
    private List<string> _logs;

    [TearDown]
    public void TearDown()
    {
        _mockCommand?.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        _mockHelper = Substitute.For<IMergeScriptHelper>();
        _mockCommand = Substitute.For<IDbCommand>();
        _executedScripts = new List<string>();
        _logs = new List<string>();

        _mockHelper.GetKeyColumns(Arg.Any<IDbCommand>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("Id");
        _mockHelper.BuildMergeScript(Arg.Any<IDbCommand>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(ci => $"MERGE INTO {ci.ArgAt<string>(2)}");
    }

    private DataDeliveryContext MakeContext(IList<IDeliverableTable> tables)
    {
        return new DataDeliveryContext
        {
            Tables = tables,
            Platform = "SqlServer",
            Command = _mockCommand,
            DatabaseName = "TestDB",
            TemplateRootPath = "/tmp",
            ScriptHelper = _mockHelper,
            ReadFileContent = path => "[{\"Id\": 1}]",
            ExecuteScript = (name, script) => _executedScripts.Add(script),
            ProgressLog = msg => _logs.Add(msg),
            ProgressLogError = msg => _logs.Add($"ERROR: {msg}")
        };
    }

    [Test]
    public void DeliverTables_NoTables_DoesNothing()
    {
        var processor = new DataDeliveryProcessor();
        var context = MakeContext(new List<IDeliverableTable>());

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Is.Empty);
    }

    [Test]
    public void DeliverTables_NullContext_DoesNothing()
    {
        var processor = new DataDeliveryProcessor();
        Assert.DoesNotThrow(() => processor.DeliverTables(null));
    }

    [Test]
    public void DeliverTables_MergeTypeNone_SkipsTable()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Config", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "None", ContentFile = "data.json" }
            }
        };

        processor.DeliverTables(MakeContext(tables));

        Assert.That(_executedScripts, Is.Empty);
    }

    [Test]
    public void DeliverTables_SingleTable_Delivers()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "users.json" }
            }
        };

        processor.DeliverTables(MakeContext(tables));

        Assert.That(_executedScripts, Has.Count.EqualTo(1));
        Assert.That(_executedScripts[0], Does.Contain("Users"));
    }

    [Test]
    public void DeliverTables_InvalidMergeType_ThrowsWithErrors()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "InvalidType", ContentFile = "users.json" }
            }
        };

        Assert.Throws<InvalidOperationException>(() => processor.DeliverTables(MakeContext(tables)));
    }

    [Test]
    public void DeliverTables_DependencyOrder_ParentBeforeChild()
    {
        var processor = new DataDeliveryProcessor();
        var parent = new TestTable
        {
            Name = "Customers", Schema = "dbo",
            DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "customers.json" }
        };
        var child = new TestTable
        {
            Name = "Orders", Schema = "dbo",
            DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "orders.json" },
            DeliverableColumns = new List<IDeliverableColumn>
            {
                new TestColumn { Name = "CustomerId", Nullable = false }
            },
            DeliverableForeignKeys = new List<IDeliverableForeignKey>
            {
                new TestFK { Columns = "CustomerId", RelatedTable = "Customers", RelatedTableSchema = "dbo" }
            }
        };

        processor.DeliverTables(MakeContext(new List<IDeliverableTable> { child, parent }));

        Assert.That(_executedScripts, Has.Count.EqualTo(2));
        Assert.That(_executedScripts[0], Does.Contain("Customers"));
        Assert.That(_executedScripts[1], Does.Contain("Orders"));
    }

    [Test]
    public void DeliverTables_NullableFK_UsesDeferredMerge()
    {
        var processor = new DataDeliveryProcessor();
        var parent = new TestTable
        {
            Name = "Employees", Schema = "dbo",
            DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "employees.json" }
        };
        var child = new TestTable
        {
            Name = "Tasks", Schema = "dbo",
            DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "tasks.json" },
            DeliverableColumns = new List<IDeliverableColumn>
            {
                new TestColumn { Name = "AssigneeId", Nullable = true }
            },
            DeliverableForeignKeys = new List<IDeliverableForeignKey>
            {
                new TestFK { Columns = "AssigneeId", RelatedTable = "Employees", RelatedTableSchema = "dbo" }
            }
        };

        processor.DeliverTables(MakeContext(new List<IDeliverableTable> { child, parent }));

        Assert.That(_executedScripts, Has.Count.EqualTo(3));
        Assert.That(_executedScripts[0], Does.Contain("Employees"));
        Assert.That(_executedScripts[1], Does.Contain("Tasks"));
        Assert.That(_executedScripts[2], Does.Contain("Tasks"));
    }

    [Test]
    public void DeliverTables_WhatIf_DoesNotExecute()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.WhatIf = true;

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(_logs, Has.Some.Contains("Delivering"));
        Assert.That(_logs, Has.Some.Contains("Would DELIVER:").And.Some.Contains("Users"));
    }

    [Test]
    public void ValidateDeleteCascade_MySql_EmptyTableList_ReturnsEmpty()
    {
        var errors = DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "MySQL", "testdb",
            new List<(string, string)>());

        Assert.That(errors, Is.Empty);
        _mockCommand.DidNotReceive().ExecuteReader();
    }

    [Test]
    public void ValidateDeleteCascade_MySql_NoCascadeFKs_ReturnsEmpty()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        var errors = DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "MySQL", "testdb",
            new List<(string, string)> { ("testdb", "Users") });

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateDeleteCascade_MySql_CascadeFKFound_ReturnsError()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(true, false);
        mockReader.GetString(0).Returns("FK_Orders_Users");
        mockReader.GetString(1).Returns("Orders");
        _mockCommand.ExecuteReader().Returns(mockReader);

        var errors = DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "MySQL", "testdb",
            new List<(string, string)> { ("testdb", "Users") });

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("`Users`"));
        Assert.That(errors[0], Does.Contain("`FK_Orders_Users`"));
        Assert.That(errors[0], Does.Contain("`Orders`"));
        Assert.That(errors[0], Does.Contain("CASCADE"));
    }

    [Test]
    public void ValidateDeleteCascade_MySql_UsesBinaryCaseSensitiveComparison()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "MySQL", "testdb",
            new List<(string, string)> { ("testdb", "Users") });

        Assert.That(_mockCommand.CommandText, Does.Contain("BINARY"));
        Assert.That(_mockCommand.CommandText, Does.Contain("REFERENCED_TABLE_NAME"));
    }

    [Test]
    public void ValidateDeleteCascade_MySql_DatabaseNameWithBackticks_Trimmed()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "MySQL", "`mydb`",
            new List<(string, string)> { ("`mydb`", "Users") });

        Assert.That(_mockCommand.CommandText, Does.Contain("'mydb'"));
        Assert.That(_mockCommand.CommandText, Does.Not.Contain("`mydb`"));
    }

    [Test]
    public void ValidateDeleteCascade_MySql_TableNameWithQuote_IsEscaped()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "MySQL", "testdb",
            new List<(string, string)> { ("testdb", "O'Brien") });

        Assert.That(_mockCommand.CommandText, Does.Contain("O''Brien"));
    }

    [Test]
    public void ValidateDeleteCascade_MySql_MultipleTables_QueriesEachOne()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "MySQL", "testdb",
            new List<(string, string)> { ("testdb", "Users"), ("testdb", "Orders") });

        _mockCommand.Received(2).ExecuteReader();
    }

    [Test]
    public void ValidateDeleteCascade_SqlServer_UsesStandardJoinQuery()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "SqlServer", "testdb",
            new List<(string, string)> { ("dbo", "Users") });

        Assert.That(_mockCommand.CommandText, Does.Contain("INFORMATION_SCHEMA.TABLE_CONSTRAINTS"));
        Assert.That(_mockCommand.CommandText, Does.Contain("'dbo'"));
        Assert.That(_mockCommand.CommandText, Does.Contain("'Users'"));
        Assert.That(_mockCommand.CommandText, Does.Not.Contain("BINARY"));
        Assert.That(_mockCommand.CommandText, Does.Not.Contain("REFERENCED_TABLE_NAME"));
    }

    [Test]
    public void ValidateDeleteCascade_SqlServer_CascadeFKFound_ErrorUsesBracketQuoting()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(true, false);
        mockReader.GetString(0).Returns("FK_Orders_Users");
        mockReader.GetString(1).Returns("Orders");
        _mockCommand.ExecuteReader().Returns(mockReader);

        var errors = DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "SqlServer", "testdb",
            new List<(string, string)> { ("dbo", "Users") });

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("[dbo].[Users]"));
        Assert.That(errors[0], Does.Contain("[FK_Orders_Users]"));
        Assert.That(errors[0], Does.Contain("[Orders]"));
        Assert.That(errors[0], Does.Contain("CASCADE"));
    }

    [Test]
    public void ValidateDeleteCascade_SqlServer_SchemaWithQuote_IsEscaped()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "SqlServer", "testdb",
            new List<(string, string)> { ("s'chema", "Users") });

        Assert.That(_mockCommand.CommandText, Does.Contain("s''chema"));
    }

    [Test]
    public void ValidateDeleteCascade_PostgreSql_UsesStandardJoinQuery()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "PostgreSQL", "testdb",
            new List<(string, string)> { ("public", "Users") });

        Assert.That(_mockCommand.CommandText, Does.Contain("INFORMATION_SCHEMA.TABLE_CONSTRAINTS"));
        Assert.That(_mockCommand.CommandText, Does.Contain("'public'"));
        Assert.That(_mockCommand.CommandText, Does.Contain("'Users'"));
        Assert.That(_mockCommand.CommandText, Does.Not.Contain("BINARY"));
    }

    [Test]
    public void ValidateDeleteCascade_PostgreSql_CascadeFKFound_ErrorUsesDoubleQuoteQuoting()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(true, false);
        mockReader.GetString(0).Returns("FK_Orders_Users");
        mockReader.GetString(1).Returns("Orders");
        _mockCommand.ExecuteReader().Returns(mockReader);

        var errors = DataDeliveryProcessor.ValidateDeleteCascade(_mockCommand, "PostgreSQL", "testdb",
            new List<(string, string)> { ("public", "Users") });

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("\"public\".\"Users\""));
        Assert.That(errors[0], Does.Contain("\"FK_Orders_Users\""));
        Assert.That(errors[0], Does.Contain("\"Orders\""));
        Assert.That(errors[0], Does.Contain("CASCADE"));
    }

    [Test]
    public void DeliverTables_MySqlPlatform_WithDeleteMergeType_ValidatesCascade()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "",
                DataDelivery = new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "MySQL";

        processor.DeliverTables(context);

        _mockCommand.Received().ExecuteReader();
        Assert.That(_executedScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void DeliverTables_SqlServerPlatform_WithDeleteMergeType_ValidatesCascade()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "SqlServer";

        processor.DeliverTables(context);

        _mockCommand.Received().ExecuteReader();
        Assert.That(_mockCommand.CommandText, Does.Contain("INFORMATION_SCHEMA.TABLE_CONSTRAINTS"));
        Assert.That(_executedScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void DeliverTables_PostgreSqlPlatform_WithDeleteMergeType_ValidatesCascade()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "public",
                DataDelivery = new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "PostgreSQL";

        processor.DeliverTables(context);

        _mockCommand.Received().ExecuteReader();
        Assert.That(_mockCommand.CommandText, Does.Contain("INFORMATION_SCHEMA.TABLE_CONSTRAINTS"));
        Assert.That(_executedScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void DeliverTables_SqlServerPlatform_CascadeFKDetected_AbortsWithError()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(true, false);
        mockReader.GetString(0).Returns("FK_Orders_Users");
        mockReader.GetString(1).Returns("Orders");
        _mockCommand.ExecuteReader().Returns(mockReader);

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "SqlServer";

        Assert.Throws<System.InvalidOperationException>(() => processor.DeliverTables(context));
        Assert.That(_executedScripts, Is.Empty, "Delivery must abort before any script runs");
        Assert.That(_logs, Has.Some.Contains("CASCADE"));
    }

    [Test]
    public void DeliverTables_NullReadFileContent_SkipsTable()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.ReadFileContent = null;

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(_logs, Has.Some.Contains("ERROR:").And.Some.Contains("SKIPPING"));
    }

    [Test]
    public void DeliverTables_ReadFileContentReturnsNull_SkipsTable()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.ReadFileContent = _ => null;

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(_logs, Has.Some.Contains("ERROR:").And.Some.Contains("SKIPPING"));
    }

    [Test]
    public void DeliverTables_ReadFileContentThrows_SkipsTable()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.ReadFileContent = _ => throw new System.IO.IOException("File not found");

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(_logs, Has.Some.Contains("ERROR:").And.Some.Contains("SKIPPING"));
    }

    [Test]
    public void DeliverTables_MySqlPlatform_GetSchemaOrDb_ReturnsDatabaseName()
    {
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "custom_schema",
                DataDelivery = new DataDelivery { MergeType = "Insert", ContentFile = "users.json" }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "MySQL";
        context.DatabaseName = "MyDatabase";

        processor.DeliverTables(context);

        _mockHelper.Received().BuildMergeScript(
            Arg.Any<IDbCommand>(),
            Arg.Is("MyDatabase"),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>());
    }

    [Test]
    public void GetFromFactory_ReturnsIDataDeliveryInstance()
    {
        var delivery = DataDeliveryProcessor.GetFromFactory();

        Assert.That(delivery, Is.Not.Null);
        Assert.That(delivery, Is.InstanceOf<IDataDelivery>());
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
        public IReadOnlyList<DataDelivery> DataDeliveries { get; set; } = new List<DataDelivery>();
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "None", ContentFile = "data.json" } }
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };

        processor.DeliverTables(MakeContext(tables));

        Assert.That(_executedScripts, Has.Count.EqualTo(1));
        Assert.That(_executedScripts[0], Does.Contain("Users"));
    }

    [Test]
    public void DeliverTables_TwoDeliveriesOneTable_DeliversBothInOrder()
    {
        // Both deliveries carry a non-blank ShouldApplyExpression, so each is gate-evaluated
        // against _mockCommand.ExecuteScalar() — make both gates pass so this test still
        // exercises "two distinct deliveries fire in order", independent of gating.
        _mockCommand.ExecuteScalar().Returns(1);
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Ref", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery { MergeType = "Insert", ContentFile = "a.json", ShouldApplyExpression = "1=1", VariantName = "a" },
                    new DataDelivery { MergeType = "Insert", ContentFile = "b.json", ShouldApplyExpression = "1=1", VariantName = "b" }
                }
            }
        };

        var context = MakeContext(tables);
        // The shared [SetUp] mocks return identical content/scripts for both deliveries, which
        // can't distinguish "ran two distinct deliveries in order" from "ran one twice". Make
        // content encode the resolved content-file path, and echo that content (tableData, arg
        // index 3) through BuildMergeScript's output so the emitted script reveals which
        // delivery actually produced it.
        context.ReadFileContent = path => $"CONTENT:{path}";
        _mockHelper.BuildMergeScript(Arg.Any<IDbCommand>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(ci => $"MERGE INTO {ci.ArgAt<string>(2)} USING {ci.ArgAt<string>(3)}");

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Has.Count.EqualTo(2));
        Assert.That(_executedScripts[0], Does.Contain("a.json").And.Not.Contain("b.json"),
            "First executed script must be the a.json delivery, not a repeat of b.json.");
        Assert.That(_executedScripts[1], Does.Contain("b.json").And.Not.Contain("a.json"),
            "Second executed script must be the b.json delivery, not a repeat of a.json.");
    }

    // ---------- ShouldApplyExpression gate evaluation (#278) ----------

    [Test]
    public void DeliverTables_GateFalse_SkipsDeliveryAndLogs()
    {
        _mockCommand.ExecuteScalar().Returns(0); // gate evaluates false
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Seed", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery { MergeType = "Insert", ContentFile = "seed.json",
                                       ShouldApplyExpression = "DB_NAME() = 'Dev'", VariantName = "dev-seed" }
                }
            }
        };

        processor.DeliverTables(MakeContext(tables));

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(_logs, Has.Some.Contains("Skipping data delivery").And.Some.Contains("dev-seed"));
    }

    [Test]
    public void DeliverTables_TwoVariants_OnlyGatePassingApplies()
    {
        // First variant's gate true, second false: use a sequenced scalar.
        _mockCommand.ExecuteScalar().Returns(1, 0);
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Ref", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery { MergeType = "Insert", ContentFile = "dev.json",  ShouldApplyExpression = "DB_NAME()='Dev'",  VariantName = "dev" },
                    new DataDelivery { MergeType = "Insert", ContentFile = "prod.json", ShouldApplyExpression = "DB_NAME()='Prod'", VariantName = "prod" }
                }
            }
        };

        processor.DeliverTables(MakeContext(tables));

        Assert.That(_executedScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void DeliverTables_GateThrows_PropagatesFailClosed()
    {
        _mockCommand.ExecuteScalar().Returns(_ => throw new Exception("bad gate"));
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Seed", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery { MergeType = "Insert", ContentFile = "seed.json", ShouldApplyExpression = "broken", VariantName = "x" }
                }
            }
        };

        Assert.Throws<Exception>(() => processor.DeliverTables(MakeContext(tables)));
    }

    [Test]
    public void DeliverTables_SchemaTemplate_SubstitutesIterationSchemaIntoGateExpression()
    {
        // The gate expression is verbatim text handed to GateEvaluator.ShouldApply, which sets
        // it as command.CommandText and executes it directly against the server — there is no
        // engine-level token resolution between here and the database. Mirrors the MergeFilter
        // bug (slice 7): if a schema-template Table.json's ShouldApplyExpression contains
        // {{SchemaName}}, the literal token must not reach the server.
        string capturedCommandText = null;
        _mockCommand.ExecuteScalar().Returns(ci =>
        {
            capturedCommandText = _mockCommand.CommandText;
            return 1;
        });

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Lookups", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery
                    {
                        MergeType = "Insert",
                        ContentFile = "lookups.json",
                        ShouldApplyExpression = "SCHEMA_NAME() = '{{SchemaName}}'",
                        VariantName = "tenant-gate"
                    }
                }
            }
        };
        var context = MakeContext(tables);
        context.SchemaName = "tenant_acme";

        processor.DeliverTables(context);

        Assert.That(capturedCommandText, Does.Contain("tenant_acme"),
            "ShouldApplyExpression must have {{SchemaName}} substituted with the iteration's resolved schema.");
        Assert.That(capturedCommandText, Does.Not.Contain("{{SchemaName}}"),
            "Literal {{SchemaName}} must never reach the gate evaluation command text.");
        Assert.That(_executedScripts, Has.Count.EqualTo(1), "Gate evaluated true, so the delivery must apply.");
    }

    [Test]
    public void DeliverTables_RegularTemplate_GateExpressionPassedThroughUnchanged()
    {
        // Regression guard: regular templates (SchemaName empty) must not touch
        // ShouldApplyExpression, mirroring DeliverTables_RegularTemplate_MergeFilterPassedThroughUnchanged.
        string capturedCommandText = null;
        _mockCommand.ExecuteScalar().Returns(ci =>
        {
            capturedCommandText = _mockCommand.CommandText;
            return 1;
        });

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Lookups", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery
                    {
                        MergeType = "Insert",
                        ContentFile = "lookups.json",
                        ShouldApplyExpression = "SCHEMA_NAME() = '{{SchemaName}}'",
                        VariantName = "tenant-gate"
                    }
                }
            }
        };
        var context = MakeContext(tables); // SchemaName defaults to ""

        processor.DeliverTables(context);

        Assert.That(capturedCommandText, Does.Contain("{{SchemaName}}"),
            "Regular templates (blank SchemaName) must leave the gate expression's literal token unchanged.");
    }

    [Test]
    public void DeliverTables_GatedDelivery_Applies_LogsVariantNameSuffix()
    {
        // Fix 2 (#278 QA finding): the apply/"Delivering" log line must echo VariantName the
        // same way the skip line already does, for consistency and debuggability.
        _mockCommand.ExecuteScalar().Returns(1);
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Seed", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery { MergeType = "Insert", ContentFile = "seed.json",
                                       ShouldApplyExpression = "1=1", VariantName = "dev-seed" }
                }
            }
        };

        processor.DeliverTables(MakeContext(tables));

        Assert.That(_logs, Has.Some.Match(@"Delivering dbo\.Seed \[dev-seed\]"));
    }

    [Test]
    public void DeliverTables_UngatedDelivery_BlankVariantName_LogsPlainDeliveringLine()
    {
        // Regression guard: a single ungated delivery (blank VariantName) must keep logging the
        // exact plain "Delivering {tableKey}" line — no suffix — so existing behavior/tests for
        // the common case are unaffected.
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };

        processor.DeliverTables(MakeContext(tables));

        Assert.That(_logs, Has.Some.EqualTo("    Delivering dbo.Users"));
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "InvalidType", ContentFile = "users.json" } }
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
            DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "customers.json" } }
        };
        var child = new TestTable
        {
            Name = "Orders", Schema = "dbo",
            DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "orders.json" } },
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
            DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "employees.json" } }
        };
        var child = new TestTable
        {
            Name = "Tasks", Schema = "dbo",
            DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "tasks.json" } },
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" } }
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" } }
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" } }
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json" } }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "SqlServer";

        Assert.Throws<System.InvalidOperationException>(() => processor.DeliverTables(context));
        Assert.That(_executedScripts, Is.Empty, "Delivery must abort before any script runs");
        Assert.That(_logs, Has.Some.Contains("CASCADE"));
    }

    [Test]
    public void DeliverTables_DeleteMergeType_GateFalse_DoesNotTriggerCascadeAbort()
    {
        // The single Delete delivery's gate evaluates false (ExecuteScalar -> 0), so it will NOT
        // run in this environment. Even though the cascade query (ExecuteReader) would report a
        // CASCADE-deleting FK on this table, a gated-off delivery must not abort the whole run
        // (#278 false-abort fix). Under the pre-fix code, ValidateDeleteCascade runs over the
        // ungated ApplicableDeliveries set and would throw before the gate is ever evaluated.
        _mockCommand.ExecuteScalar().Returns(0);
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
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery { MergeType = "Insert/Update/Delete", ContentFile = "users.json",
                                       ShouldApplyExpression = "DB_NAME() = 'Prod'", VariantName = "prod-only" }
                }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "SqlServer";

        Assert.DoesNotThrow(() => processor.DeliverTables(context));

        Assert.That(_executedScripts, Is.Empty, "Gated-off delivery must not execute");
        _mockCommand.DidNotReceive().ExecuteReader();
        Assert.That(_logs, Has.Some.Contains("Skipping data delivery").And.Some.Contains("prod-only"));
    }

    [Test]
    public void DeliverTables_NullReadFileContent_AbortsDelivery()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };
        var context = MakeContext(tables);
        context.ReadFileContent = null;

        var ex = Assert.Throws<InvalidOperationException>(() => processor.DeliverTables(context));

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(ex!.Message, Does.Contain("Unable to read"));
        Assert.That(_logs, Has.Some.Contains("ERROR:").And.Some.Contains("No content file reader configured"));
        Assert.That(_logs, Has.None.Contains("SKIPPING"));
    }

    [Test]
    public void DeliverTables_ReadFileContentReturnsNull_AbortsDelivery()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };
        var context = MakeContext(tables);
        context.ReadFileContent = _ => null;

        var ex = Assert.Throws<InvalidOperationException>(() => processor.DeliverTables(context));

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(ex!.Message, Does.Contain("Unable to read"));
        Assert.That(_logs, Has.Some.Contains("ERROR:").And.Some.Contains("Content file not found"));
        Assert.That(_logs, Has.None.Contains("SKIPPING"));
    }

    [Test]
    public void DeliverTables_WhatIfWithMissingContentFile_AbortsLikeRealDeployment()
    {
        // The content-file read loop runs before the per-table WhatIf gate, so a missing content
        // file aborts a WhatIf dry-run just like a real deployment (fail loud during the dry-run).
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };
        var context = MakeContext(tables);
        context.WhatIf = true;
        context.ReadFileContent = _ => null;

        var ex = Assert.Throws<InvalidOperationException>(() => processor.DeliverTables(context));

        Assert.That(ex!.Message, Does.Contain("Unable to read"));
        Assert.That(_executedScripts, Is.Empty);
    }

    [Test]
    public void DeliverTables_ReadFileContentThrows_AbortsDelivery()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };
        var context = MakeContext(tables);
        context.ReadFileContent = _ => throw new System.IO.IOException("File not found");

        var ex = Assert.Throws<InvalidOperationException>(() => processor.DeliverTables(context));

        Assert.That(_executedScripts, Is.Empty);
        Assert.That(ex!.Message, Does.Contain("Unable to read"));
        Assert.That(_logs, Has.Some.Contains("ERROR:").And.Some.Contains("Error reading content file"));
        Assert.That(_logs, Has.None.Contains("SKIPPING"));
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
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
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

    // ---------- Schema-template token substitution (slice-3 audit B3) ----------
    //
    // Schema-template tables carry table.Schema = "{{SchemaName}}" (set by slice-1's
    // SchemaDefaultResolver). DataDeliveryProcessor must substitute the iteration's
    // resolved schema name (DataDeliveryContext.SchemaName) before passing it to the
    // metadata catalog probes and MERGE-script builder; otherwise the literal token
    // becomes the schema name in catalog queries (returning empty result sets) and
    // in the emitted MERGE INTO clause.

    [Test]
    public void DeliverTables_SchemaTemplate_SubstitutesIterationSchemaIntoMergeScript()
    {
        // Override the default mock to surface the resolved schema name in the emitted
        // script so the assertion below checks both the call argument AND that no token
        // literal can sneak into a downstream-rendered script.
        _mockHelper.BuildMergeScript(Arg.Any<IDbCommand>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(ci => $"MERGE INTO [{ci.ArgAt<string>(1)}].[{ci.ArgAt<string>(2)}] AS Target");

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Lookups", Schema = "{{SchemaName}}",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert/Update", ContentFile = "lookups.json" } }
            }
        };
        var context = MakeContext(tables);
        context.SchemaName = "tenant_acme";

        processor.DeliverTables(context);

        // The processor must pass the resolved iteration schema ("tenant_acme") in place
        // of the literal "{{SchemaName}}" to the IMergeScriptHelper.
        Assert.That(_executedScripts, Has.Count.EqualTo(1));
        Assert.That(_executedScripts[0], Does.Contain("[tenant_acme].[Lookups]"),
            "Schema-template iteration must substitute {{SchemaName}} before emit.");
        Assert.That(_executedScripts[0], Does.Not.Contain("{{SchemaName}}"),
            "Literal {{SchemaName}} must never appear in an emitted merge script.");
        _mockHelper.Received().BuildMergeScript(
            Arg.Any<IDbCommand>(),
            Arg.Is("tenant_acme"),
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
    public void DeliverTables_SchemaTemplate_SubstitutesIterationSchemaIntoKeyColumnsLookup()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Lookups", Schema = "{{SchemaName}}",
                // No MatchColumns -> processor falls back to GetKeyColumns, which probes catalog.
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "lookups.json" } }
            }
        };
        var context = MakeContext(tables);
        context.SchemaName = "tenant_beta";

        processor.DeliverTables(context);

        // GetKeyColumns runs a catalog query — if {{SchemaName}} reaches it as a literal,
        // the query returns no rows and the merge is silently broken.
        _mockHelper.Received().GetKeyColumns(
            Arg.Any<IDbCommand>(),
            Arg.Is("tenant_beta"),
            Arg.Is("Lookups"));
    }

    [Test]
    public void DeliverTables_RegularTemplate_DefaultSchemaName_UnchangedBehavior()
    {
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };
        var context = MakeContext(tables); // SchemaName defaults to ""

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Has.Count.EqualTo(1));
        _mockHelper.Received().BuildMergeScript(
            Arg.Any<IDbCommand>(),
            Arg.Is("dbo"),
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
    public void DataDeliveryContext_SchemaName_DefaultsToEmptyString()
    {
        var ctx = new DataDeliveryContext();
        Assert.That(ctx.SchemaName, Is.EqualTo(string.Empty),
            "Regular-template callers must not need to set SchemaName.");
    }

    // ---------- WriteResolvedSqlArtifact callback on merge failure ----------

    [Test]
    public void DeliverTables_OnMergeFailure_InvokesWriteResolvedSqlArtifact_WithFailingSql()
    {
        // Arrange: ExecuteScript throws for the target table so the processor reaches the
        // merge-failure catch path. Record every callback invocation.
        var invocations = new List<(string Label, string Sql)>();

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Orders", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "orders.json" } }
            }
        };

        var context = MakeContext(tables);
        context.ExecuteScript = (name, script) => throw new Exception("Simulated DB failure");
        context.WriteResolvedSqlArtifact = (label, sql) => invocations.Add((label, sql));

        // Act: delivery aborts after the failure (circular-fallback catch logs and swallows per-table)
        processor.DeliverTables(context);

        // Assert: callback fired EXACTLY ONCE despite the table being attempted in both the while
        // loop and the circular-fallback loop, and carried the table key + generated merge SQL.
        Assert.That(invocations, Has.Count.EqualTo(1),
            "WriteResolvedSqlArtifact must fire exactly once per failed table, not once per attempt");
        Assert.That(invocations[0].Label, Does.Contain("Orders"), "Label must identify the failing table");
        Assert.That(invocations[0].Sql, Is.Not.Null.And.Not.Empty, "Captured SQL must be non-empty");
        Assert.That(invocations[0].Sql, Does.Contain("MERGE INTO"), "Captured SQL must be the generated merge script");
    }

    [Test]
    public void DeliverTables_OnDeferredMergeFailure_InvokesWriteResolvedSqlArtifactOnce()
    {
        // Drives the pass-1 (deferred-column) merge path: a child with a nullable FK to a parent
        // delivers via the deferred merge first. ExecuteScript throws on the child's merge,
        // exercising the Site-A (deferred) catch. Assert the callback fires exactly once.
        var invocations = new List<(string Label, string Sql)>();

        var processor = new DataDeliveryProcessor();
        var parent = new TestTable
        {
            Name = "Employees", Schema = "dbo",
            DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "employees.json" } }
        };
        var child = new TestTable
        {
            Name = "Tasks", Schema = "dbo",
            DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "tasks.json" } },
            DeliverableColumns = new List<IDeliverableColumn>
            {
                new TestColumn { Name = "AssigneeId", Nullable = true }
            },
            DeliverableForeignKeys = new List<IDeliverableForeignKey>
            {
                new TestFK { Columns = "AssigneeId", RelatedTable = "Employees", RelatedTableSchema = "dbo" }
            }
        };

        var context = MakeContext(new List<IDeliverableTable> { child, parent });
        // Fail only the child's merges (the parent delivers fine); the child's pass-1 deferred merge
        // is the failing one and reaches the Site-A catch.
        context.ExecuteScript = (name, script) =>
        {
            if (name == "Tasks") throw new Exception("Simulated deferred-merge failure");
        };
        context.WriteResolvedSqlArtifact = (label, sql) => invocations.Add((label, sql));

        processor.DeliverTables(context);

        Assert.That(invocations, Has.Count.EqualTo(1),
            "Deferred-path failure must write exactly one artifact for the failing table");
        Assert.That(invocations[0].Label, Does.Contain("Tasks"));
        Assert.That(invocations[0].Sql, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void DeliverTables_OnMergeFailure_ExistingErrorLogStillFires()
    {
        // Regression guard: adding the artifact callback must not suppress the existing error log.
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Products", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "products.json" } }
            }
        };

        var context = MakeContext(tables);
        context.ExecuteScript = (name, script) => throw new Exception("DB error");
        context.WriteResolvedSqlArtifact = (_, _) => { };

        processor.DeliverTables(context);

        Assert.That(_logs, Has.Some.Contains("ERROR:").And.Some.Contains("Error delivering"));
    }

    [Test]
    public void DeliverTables_OnMergeFailure_NullCallback_DoesNotThrow()
    {
        // Null-guard: WhatIf callers and any caller that didn't wire the callback must be unaffected.
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };

        var context = MakeContext(tables);
        context.ExecuteScript = (name, script) => throw new Exception("DB error");
        context.WriteResolvedSqlArtifact = null;

        Assert.DoesNotThrow(() => processor.DeliverTables(context),
            "Null WriteResolvedSqlArtifact must not cause a NullReferenceException");
    }

    [Test]
    public void DeliverTables_SucceededSiblingDelivery_NotReRunWhenLaterDeliveryFailsThenRetries()
    {
        var processor = new DataDeliveryProcessor();

        // Embed the tableData in the generated script so delivery A and B are distinguishable.
        _mockHelper.BuildMergeScript(Arg.Any<IDbCommand>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(ci => $"MERGE {ci.ArgAt<string>(3)}");

        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery { MergeType = "Insert", ContentFile = "a.json" },
                    new DataDelivery { MergeType = "Insert", ContentFile = "b.json" }
                }
            }
        };

        var executed = new List<string>();
        var bHasThrown = false;
        var context = new DataDeliveryContext
        {
            Tables = tables,
            Platform = "SqlServer",
            Command = _mockCommand,
            DatabaseName = "TestDB",
            TemplateRootPath = "/tmp",
            ScriptHelper = _mockHelper,
            ReadFileContent = path => path!.Replace('\\', '/').EndsWith("a.json") ? "[{\"Id\":1}]" : "[{\"Id\":2}]",
            ExecuteScript = (name, script) =>
            {
                executed.Add(script);
                if (script.Contains("\"Id\":2") && !bHasThrown) { bHasThrown = true; throw new Exception("B fails on first attempt"); }
            },
            ProgressLog = _ => { },
            ProgressLogError = _ => { }
        };

        processor.DeliverTables(context);

        Assert.That(executed.FindAll(s => s.Contains("\"Id\":1")).Count, Is.EqualTo(1),
            "The succeeded first delivery must not re-run when the second delivery's failure re-queues the table.");
        Assert.That(executed.FindAll(s => s.Contains("\"Id\":2")).Count, Is.EqualTo(2),
            "The failing second delivery runs twice (fail, then success on retry).");
    }

    // ---------- ResolveContentFilePath separator normalization ----------
    //
    // ContentFile paths may use either separator style; resolution normalizes both to the platform
    // separator so output is native regardless of which OS runs the deployment. The CI matrix runs
    // both OSes, so these input styles cover all four input/platform combinations.
    // The method now returns the fully-resolved absolute path (Path.GetFullPath), so tests use an
    // absolute root and verify the result ends with the expected sub-path suffix.

    private static string SepNormRoot =>
        Path.Combine(Path.GetTempPath(), "ssmith_sep_" + Guid.NewGuid().ToString("N"));

    [Test]
    public void ResolveContentFilePath_WindowsStyleInput_ProducesPlatformNativePath()
    {
        var root = SepNormRoot;
        var result = DataDeliveryProcessor.ResolveContentFilePath(root, @"sub\folder\file.csv");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.EndWith(Path.Combine("sub", "folder", "file.csv")));
    }

    [Test]
    public void ResolveContentFilePath_LinuxStyleInput_ProducesPlatformNativePath()
    {
        var root = SepNormRoot;
        var result = DataDeliveryProcessor.ResolveContentFilePath(root, "sub/folder/file.csv");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.EndWith(Path.Combine("sub", "folder", "file.csv")));
    }

    [Test]
    public void ResolveContentFilePath_MixedSeparators_ProducesPlatformNativePath()
    {
        var root = SepNormRoot;
        var result = DataDeliveryProcessor.ResolveContentFilePath(root, @"sub\folder/file.csv");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.EndWith(Path.Combine("sub", "folder", "file.csv")));
    }

    [Test]
    public void ResolveContentFilePath_EmptyOrNullInput_ReturnsNull()
    {
        Assert.That(DataDeliveryProcessor.ResolveContentFilePath("root", ""), Is.Null);
        Assert.That(DataDeliveryProcessor.ResolveContentFilePath("root", null), Is.Null);
        Assert.That(DataDeliveryProcessor.ResolveContentFilePath("", "file.csv"), Is.Null);
        Assert.That(DataDeliveryProcessor.ResolveContentFilePath(null, "file.csv"), Is.Null);
    }

    // ---------- ResolveContentFilePath path-traversal containment ----------
    //
    // ContentFile may come from an externally-authored template. A malicious value
    // can use ".." to escape the template root or an absolute path to bypass it
    // entirely. The containment guard must reject both attack shapes and return null
    // (which routes into the caller's existing "Unable to locate content file" error
    // path). These tests are written cross-platform: they use Path helpers rather
    // than hard-coded Windows or Unix paths so CI passes on both Windows and Linux.

    private static string _tplRoot =>
        Path.Combine(Path.GetTempPath(), "ssmith_tpl_" + Guid.NewGuid().ToString("N"));

    [Test]
    public void ResolveContentFilePath_NormalRelativePath_ReturnsContainedPath()
    {
        var root = _tplRoot;
        var sep = Path.DirectorySeparatorChar.ToString();
        var contentFile = "sub" + sep + "file.sql";

        var result = DataDeliveryProcessor.ResolveContentFilePath(root, contentFile);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.StartWith(Path.GetFullPath(root) + sep));
    }

    [Test]
    public void ResolveContentFilePath_ParentEscape_ReturnsNull()
    {
        var root = _tplRoot;
        var sep = Path.DirectorySeparatorChar.ToString();
        var contentFile = ".." + sep + "outside.sql";

        var result = DataDeliveryProcessor.ResolveContentFilePath(root, contentFile);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveContentFilePath_DeepEscapeWithBackslashes_ReturnsNull()
    {
        var root = _tplRoot;

        var result = DataDeliveryProcessor.ResolveContentFilePath(root, @"..\..\..\x");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveContentFilePath_AbsoluteContentFile_ReturnsNull()
    {
        var root = _tplRoot;
        var absoluteContentFile = Path.Combine(Path.GetTempPath(), "evil.sql");

        var result = DataDeliveryProcessor.ResolveContentFilePath(root, absoluteContentFile);

        Assert.That(result, Is.Null);
    }

    // ---------- MergeFilter token substitution (slice 7) ----------
    //
    // delivery.MergeFilter is user-supplied text that gets embedded into the merge body's
    // `WHEN NOT MATCHED BY SOURCE AND ({mergeFilter})` clause and shipped straight to the
    // database via ExecuteScript — no engine-level token resolution between the helper and
    // the execute. The slice-3 audit B3 fix only addressed table.Schema; if a schema-template
    // Table.json's MergeFilter contains {{SchemaName}}, that literal token reaches the
    // server and produces a SQL parse error. This pair of tests asserts that MergeFilter is
    // token-substituted with the iteration's resolved schema name BEFORE BuildMergeScript
    // sees it, mirroring the pattern already applied to table.Schema via GetSchemaOrDb.

    [Test]
    public void DeliverTables_SchemaTemplate_SubstitutesIterationSchemaIntoMergeFilter()
    {
        _mockHelper.BuildMergeScript(Arg.Any<IDbCommand>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(ci => $"MERGE INTO [{ci.ArgAt<string>(1)}].[{ci.ArgAt<string>(2)}] " +
                           $"WHEN NOT MATCHED BY SOURCE AND ({ci.ArgAt<string>(9) ?? "(none)"})");

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Lookups", Schema = "{{SchemaName}}",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery
                    {
                        MergeType = "Insert/Update/Delete",
                        ContentFile = "lookups.json",
                        MergeFilter = "Target.Status = 'Active' AND Target.[Owner] = '{{SchemaName}}'"
                    }
                }
            }
        };
        var context = MakeContext(tables);
        context.SchemaName = "tenant_acme";

        processor.DeliverTables(context);

        Assert.That(_executedScripts, Has.Count.EqualTo(1));
        Assert.That(_executedScripts[0], Does.Contain("Target.[Owner] = 'tenant_acme'"),
            "MergeFilter must have {{SchemaName}} substituted with the iteration's resolved schema.");
        Assert.That(_executedScripts[0], Does.Not.Contain("{{SchemaName}}"),
            "Literal {{SchemaName}} must never reach the executed merge script via MergeFilter.");

        _mockHelper.Received().BuildMergeScript(
            Arg.Any<IDbCommand>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Is<string>(f => f != null && f.Contains("tenant_acme") && !f.Contains("{{SchemaName}}")),
            Arg.Any<bool>(),
            Arg.Any<bool>());
    }

    [Test]
    public void DeliverTables_RegularTemplate_MergeFilterPassedThroughUnchanged()
    {
        // Regression guard: regular templates (SchemaName empty) must not touch MergeFilter.
        _mockHelper.BuildMergeScript(Arg.Any<IDbCommand>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(ci => $"MERGE INTO [{ci.ArgAt<string>(1)}] WHERE ({ci.ArgAt<string>(9) ?? "(none)"})");

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Lookups", Schema = "dbo",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery
                    {
                        MergeType = "Insert/Update/Delete",
                        ContentFile = "lookups.json",
                        MergeFilter = "Target.Status = 'Active'"
                    }
                }
            }
        };
        var context = MakeContext(tables); // SchemaName defaults to ""

        processor.DeliverTables(context);

        Assert.That(_executedScripts[0], Does.Contain("Target.Status = 'Active'"));
        _mockHelper.Received().BuildMergeScript(
            Arg.Any<IDbCommand>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Is("Target.Status = 'Active'"),
            Arg.Any<bool>(),
            Arg.Any<bool>());
    }

    [Test]
    public void DeliverTables_SchemaTemplate_NullMergeFilterStaysNull()
    {
        // Defensive guard: when MergeFilter is unset (the common case), the substitution
        // pass must not produce a non-null empty string or otherwise spuriously activate
        // the WHEN NOT MATCHED BY SOURCE delete clause.
        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Lookups", Schema = "{{SchemaName}}",
                DataDeliveries = new List<DataDelivery>
                {
                    new DataDelivery
                    {
                        MergeType = "Insert/Update",
                        ContentFile = "lookups.json",
                        MergeFilter = null
                    }
                }
            }
        };
        var context = MakeContext(tables);
        context.SchemaName = "tenant_acme";

        processor.DeliverTables(context);

        _mockHelper.Received().BuildMergeScript(
            Arg.Any<IDbCommand>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Is<string>(f => f == null),
            Arg.Any<bool>(),
            Arg.Any<bool>());
    }

    [Test]
    public void DeliverTables_SchemaTemplate_MySqlPlatform_StillReturnsDatabaseName()
    {
        // MySQL has no schema concept — GetSchemaOrDb returns DatabaseName regardless.
        // Even if a (theoretical) schema-template path set Schema = {{SchemaName}}, MySQL
        // must continue to ignore it and use the database name.
        var mockReader = Substitute.For<IDataReader>();
        mockReader.Read().Returns(false);
        _mockCommand.ExecuteReader().Returns(mockReader);

        var processor = new DataDeliveryProcessor();
        var tables = new List<IDeliverableTable>
        {
            new TestTable
            {
                Name = "Users", Schema = "{{SchemaName}}",
                DataDeliveries = new List<DataDelivery> { new DataDelivery { MergeType = "Insert", ContentFile = "users.json" } }
            }
        };
        var context = MakeContext(tables);
        context.Platform = "MySQL";
        context.DatabaseName = "tenants_db";
        context.SchemaName = "tenant_acme";

        processor.DeliverTables(context);

        _mockHelper.Received().BuildMergeScript(
            Arg.Any<IDbCommand>(),
            Arg.Is("tenants_db"),
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
}

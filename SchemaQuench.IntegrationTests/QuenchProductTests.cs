using log4net;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SchemaQuench.IntegrationTests;

public class QuenchProductTests 
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _secondaryDb;
    private readonly string _mainDb;

    public QuenchProductTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        _connectionString = ConnectionString.Build(config["Target:Server"], "master", config["Target:User"], config["Target:Password"]);
        _secondaryDb = config["ScriptTokens:SecondaryDB"];
        _mainDb = config["ScriptTokens:MainDB"];
    }

    [Test]
    public void ShouldQuenchTestProductSuccessfully()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            File.WriteAllText("SchemaQuench - Quench Tables XXX.sql", "This File To Be Deleted");
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = "../../../../TestProducts/ValidProduct";
            var product = Product.Load();

            using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @$"
INSERT SchemaSmith.CompletedMigrationScripts ([ScriptPath], [ProductName], [QuenchSlot]) VALUES('MigrationScripts/Before/MigrationScript0.sql', '{product.Name}', 'Before')
INSERT SchemaSmith.CompletedMigrationScripts ([ScriptPath], [ProductName], [QuenchSlot]) VALUES('MigrationScripts/Before/Obsolete.sql', '{product.Name}', 'Before') -- this entry should be removed from the CompletedMigrationScripts table
";
            cmd.ExecuteNonQuery();
            conn.Close();

            Quench();

            _progressLog.DidNotReceive().Error(Arg.Any<string>());
            _progressLog.Received(1).Info($"[{_mainDb}] Successfully Quenched");
            _progressLog.Received(1).Info($"[{_secondaryDb}] Successfully Quenched");
            _progressLog.Received(1).Info("Completed quench of ValidProduct");

            _environment.DidNotReceive().Exit(2);
            _environment.DidNotReceive().Exit(3);

            AssertScriptsQuenched(_mainDb);
            AssertScriptsQuenched(_secondaryDb);

            AssertCompletedMigrationsMarked(_mainDb, ExpectedMainCompletedMigrations);
            AssertCompletedMigrationsMarked(_secondaryDb, ExpectedSecondaryCompletedMigrations);

            AssertTableCreatedWithExtendedProperties(_mainDb, "dbo.TestTable");
            AssertTableCreatedWithExtendedProperties(_secondaryDb, "dbo.TestSecondaryTable");

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    [Test]
    public void ShouldErrorOnTemplateObjectScriptThatCannotBeQuenchedWithRetry()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = "../../../../TestProducts/TemplateObjectScriptError";

            Quench();

            _progressLog.Received(1).Error(Arg.Is<string>(s => s.EndsWith("Unable to quench all scripts")));
            _progressLog.Received(1).Error(Arg.Is<string>(s => s.EndsWith("One or more database quenches FAILED")));
            _environment.Received(1).Exit(2);
            _environment.DidNotReceive().Exit(3);

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    [Test]
    public void ShouldExitWithReturnCodeWhenMigrationScripErrors()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = "../../../../TestProducts/MigrationScriptError";

            Quench();

            _progressLog.Received(1).Error(Arg.Is<string>(s => s.EndsWith("KABOOM!")));
            _progressLog.Received(1).Error(Arg.Is<string>(s => s.Contains("Debug Script:") && s.Contains("MigrationScripts")));
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

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = "../../../../TestProducts/BadVersionStamp";

            Quench();

            _progressLog.Received(2).Error(Arg.Is<string>(s => s.EndsWith("BAD STAMP!")));
            _progressLog.Received(1).Error(Arg.Is<string>(s => s.EndsWith("FAILED to quench:\r\nBAD STAMP!")));
            _environment.Received(1).Exit(2);
            _environment.DidNotReceive().Exit(3);

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

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = "../../../../TestProducts/InvalidServer";

            var ex = Assert.Throws<Exception>(Quench);
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
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private void Quench()
    {
        Program.Main(["SkipKindlingForge"]);
    }

    private void AssertTableCreatedWithExtendedProperties(string dbName, string tableName)
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(dbName);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT CAST(CASE WHEN OBJECT_ID('{tableName}') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = @$"
SELECT CONVERT(VARCHAR(50), x.[value]) AS [value]
  FROM fn_listextendedproperty(default, 'Schema', 'dbo', 'Table', default, default, default) x
  WHERE objname COLLATE DATABASE_DEFAULT = '{tableName.Split(['.'])[1]}'
    AND x.[Name] COLLATE DATABASE_DEFAULT = 'ProductName'
";
        Assert.That(cmd.ExecuteScalar() as string, Is.EqualTo("ValidProduct"));
        conn.Close();
    }

    private static readonly List<string> ExpectedScriptLog =
    [
        @"Before\MigrationScript1.sql",
        "MySchema.sql",
        "Type1.sql",
        "Type2.sql",
        "MyCatalog.sql",
        "MyStoplist.sql",
        "dbo.MyFunction.sql",
        "dbo.MyView.sql",
        "dbo.MyProcedure.sql",
        "dbo.MyTrigger.sql",
        "dbo.FunctionThatNeedsView.sql", // this one will error on the first attempt and gets run again (only in Main)
        @"After\MigrationScript1.sql"
    ];

    private void AssertScriptsQuenched(string dbName)
    {
        var scriptLog = GetScriptLog(dbName, "TestLog", "Msg", "Id");

        var expected = ExpectedScriptLog.Where(l => dbName.Contains("Main") || !l.Equals("dbo.FunctionThatNeedsView.sql")).ToList();
        Assert.That(expected, Is.EquivalentTo(scriptLog)); // Validate all expected scripts are quenched in the expected order
    }

    private static readonly List<string> ExpectedMainCompletedMigrations =
    [
        "MigrationScripts/After/MigrationScript1.sql",
        "MigrationScripts/Before/MigrationScript0.sql"
    ];

    private static readonly List<string> ExpectedSecondaryCompletedMigrations =
    [
        "MigrationScripts/After/MigrationScript1.sql",
        "MigrationScripts/Before/MigrationScript1.sql"
    ];

    private void AssertCompletedMigrationsMarked(string dbName, List<string> expected)
    {
        var scriptLog = GetScriptLog(dbName, "CompletedMigrationScripts", "ScriptPath", "ScriptPath");
        Assert.That(scriptLog, Is.EquivalentTo(expected)); // Validate all expected run once migration scripts are marked as run
    }

    private List<string> GetScriptLog(string dbName, string logTable, string msgCol, string orderCol)
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT [{msgCol}] FROM [{dbName}].SchemaSmith.{logTable} WITH (NOLOCK) ORDER BY {orderCol}";
        using var reader = cmd.ExecuteReader();
        var scriptLog = new List<string>();
        while (reader.Read()) scriptLog.Add(reader[msgCol].ToString());
        conn.Close();

        return scriptLog;
    }
}

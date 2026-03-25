// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.Data.Common;
using System.IO;
using log4net;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Schema.Isolators;
using Schema.Utility;
using Tongs = SchemaTongs.SchemaTongs;

namespace SchemaTongs.UnitTests;

public class ValidateAndHandleScriptTests
{
    [Test]
    public void ShouldReturnImmediately_WhenValidateScriptsIsFalse()
    {
        var progressLog = Substitute.For<ILog>();
        var file = Substitute.For<IFile>();

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._validateScripts = false;
            tongs._templatePath = "/test/template";

            var connection = Substitute.For<DbConnection>();
            tongs.ValidateAndHandleScript(connection, "Views", "dbo.MyView.sql", "CREATE VIEW [dbo].[MyView] AS SELECT 1", "VIEW");

            file.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.DidNotReceive().Delete(Arg.Any<string>());
            progressLog.DidNotReceive().Warn(Arg.Any<string>());
            Assert.That(tongs._invalidObjects, Is.Empty);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldReturnWithoutFileOps_WhenScriptIsValid()
    {
        var progressLog = Substitute.For<ILog>();
        var file = Substitute.For<IFile>();

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._validateScripts = true;
            tongs._templatePath = "/test/template";

            // "SELECT 1" doesn't match the CREATE pattern, so RewriteWithTempName fails
            // and ValidateGuidRename returns IsValid=true immediately
            var connection = Substitute.For<DbConnection>();
            tongs.ValidateAndHandleScript(connection, "Views", "dbo.MyView.sql", "SELECT 1", "VIEW");

            file.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.DidNotReceive().Delete(Arg.Any<string>());
            progressLog.DidNotReceive().Warn(Arg.Any<string>());
            Assert.That(tongs._invalidObjects, Is.Empty);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldSaveErrorFileAndDeleteSql_WhenInvalidAndSaveEnabled()
    {
        var progressLog = Substitute.For<ILog>();
        var file = Substitute.For<IFile>();

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._validateScripts = true;
            tongs._saveInvalidScripts = true;
            tongs._templatePath = "/test/template";

            var script = "CREATE VIEW [dbo].[MyView]\r\nAS\r\nSELECT 1 AS Col";

            // Set up a DbConnection where BeginTransaction works but ExecuteNonQuery throws
            var connection = Substitute.For<DbConnection>();
            var transaction = Substitute.For<DbTransaction>();
            var command = Substitute.For<DbCommand>();

            connection.BeginTransaction().Returns(transaction);
            connection.CreateCommand().Returns(command);
            command.ExecuteNonQuery().Throws(new Exception("Invalid object name"));

            tongs.ValidateAndHandleScript(connection, "Views", "dbo.MyView.sql", script, "VIEW");

            var expectedSqlPath = Path.Combine("/test/template", "Views", "dbo.MyView.sql");
            var expectedErrorPath = Path.ChangeExtension(expectedSqlPath, ".sqlerror");

            file.Received(1).WriteAllText(expectedErrorPath, script);
            file.Received(1).Delete(expectedSqlPath);
            progressLog.Received(1).Warn(Arg.Is<string>(s => s.Contains("dbo.MyView.sql") && s.Contains("Invalid object name")));
            Assert.That(tongs._invalidObjects, Has.Count.EqualTo(1));
            Assert.That(tongs._invalidObjects[0].FileName, Is.EqualTo("dbo.MyView.sql"));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldDeleteBothFiles_WhenInvalidAndSaveDisabled()
    {
        var progressLog = Substitute.For<ILog>();
        var file = Substitute.For<IFile>();

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._validateScripts = true;
            tongs._saveInvalidScripts = false;
            tongs._templatePath = "/test/template";

            var script = "CREATE VIEW [dbo].[MyView]\r\nAS\r\nSELECT 1 AS Col";

            var connection = Substitute.For<DbConnection>();
            var transaction = Substitute.For<DbTransaction>();
            var command = Substitute.For<DbCommand>();

            connection.BeginTransaction().Returns(transaction);
            connection.CreateCommand().Returns(command);
            command.ExecuteNonQuery().Throws(new Exception("Syntax error"));

            var expectedSqlPath = Path.Combine("/test/template", "Views", "dbo.MyView.sql");
            var expectedErrorPath = Path.ChangeExtension(expectedSqlPath, ".sqlerror");

            // When save is disabled and error file exists, both should be deleted
            file.Exists(expectedErrorPath).Returns(true);

            tongs.ValidateAndHandleScript(connection, "Views", "dbo.MyView.sql", script, "VIEW");

            file.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(1).Delete(expectedSqlPath);
            file.Received(1).Delete(expectedErrorPath);
            progressLog.Received(1).Warn(Arg.Is<string>(s => s.Contains("dbo.MyView.sql")));
            Assert.That(tongs._invalidObjects, Has.Count.EqualTo(1));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldUseFallbackPath_WhenNoFolderIndex()
    {
        var progressLog = Substitute.For<ILog>();
        var file = Substitute.For<IFile>();

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._validateScripts = true;
            tongs._saveInvalidScripts = true;
            tongs._templatePath = "/test/template";
            // _folderIndexes is empty by default, so no index for "Views"

            var script = "CREATE PROCEDURE [dbo].[usp_Test]\r\nAS\r\nSELECT 1";

            var connection = Substitute.For<DbConnection>();
            var transaction = Substitute.For<DbTransaction>();
            var command = Substitute.For<DbCommand>();

            connection.BeginTransaction().Returns(transaction);
            connection.CreateCommand().Returns(command);
            command.ExecuteNonQuery().Throws(new Exception("Could not find stored procedure"));

            tongs.ValidateAndHandleScript(connection, "Procedures", "dbo.usp_Test.sql", script, "PROCEDURE");

            // With no folder index, path falls back to Path.Combine(_templatePath, folderName, fileName)
            var expectedSqlPath = Path.Combine("/test/template", "Procedures", "dbo.usp_Test.sql");
            var expectedErrorPath = Path.ChangeExtension(expectedSqlPath, ".sqlerror");

            file.Received(1).WriteAllText(expectedErrorPath, script);
            file.Received(1).Delete(expectedSqlPath);
            Assert.That(tongs._invalidObjects, Has.Count.EqualTo(1));
            Assert.That(tongs._invalidObjects[0].Folder, Is.EqualTo("Procedures"));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}

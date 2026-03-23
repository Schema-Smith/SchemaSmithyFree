// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Schema.Domain;

﻿using Schema.Domain;
using Schema.Isolators;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;

namespace Schema.UnitTests;

public class SqlScriptTests
{
    [Test]
    public void ShouldErrorOnBadSqlScriptPath()
    {
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => SqlScript.Load("badPath"));
            Assert.That(ex!.Message, Is.EqualTo("File badPath does not exist"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldLoadSqlScriptSuccessfully()
    {
        var filePath = "scripts/Setup.sql";
        const string sqlContent = "SELECT 1\nGO\nSELECT 2\nGO";

        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(filePath).Returns(true);
        mockFileWrapper.ReadAllText(filePath).Returns(sqlContent);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var script = SqlScript.Load(filePath);

            Assert.Multiple(() =>
            {
                Assert.That(script, Is.Not.Null);
                Assert.That(script.Name, Is.EqualTo("Setup.sql"));
                Assert.That(script.FilePath, Is.EqualTo(filePath));
                Assert.That(script.Batches, Has.Count.EqualTo(2));
                Assert.That(script.HasBeenQuenched, Is.False);
                Assert.That(script.Error, Is.Null);
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldWrapExceptionWithFilePathWhenSqlParsingFails()
    {
        var filePath = "scripts/Broken.sql";
        // Unterminated string — will cause SplitIntoBatches to throw
        const string brokenSql = "DECLARE @x VARCHAR(100) = '\nGO";

        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(filePath).Returns(true);
        mockFileWrapper.ReadAllText(filePath).Returns(brokenSql);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => SqlScript.Load(filePath));
            Assert.That(ex!.Message, Does.Contain("Broken.sql"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void LogPathStripsLongPathPrefix()
    {
        const string filePath = @"\\?\C:\deep\path\Script.sql";

        var script = new SqlScript { FilePath = filePath };

        // LogPath should strip the \\?\ prefix via LongPathSupport
        Assert.That(script.LogPath, Does.Not.StartWith(@"\\?\"));
    }

    [Test]
    public void ShouldLoadScriptWithSingleBatch()
    {
        var filePath = "scripts/Single.sql";
        const string sqlContent = "CREATE TABLE dbo.Test (Id INT)";

        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(filePath).Returns(true);
        mockFileWrapper.ReadAllText(filePath).Returns(sqlContent);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var script = SqlScript.Load(filePath);

            Assert.That(script.Batches, Has.Count.EqualTo(1));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldWrapExceptionWithFilePathWhenReadAllTextFails()
    {
        // Exercises the catch block when the IO layer throws rather than the SQL parser
        var filePath = "scripts/Unreadable.sql";

        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(filePath).Returns(true);
        mockFileWrapper.ReadAllText(filePath).Returns(_ => throw new IOException("disk error"));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => SqlScript.Load(filePath));
            Assert.That(ex!.Message, Does.Contain("Unreadable.sql"));
            Assert.That(ex.Message, Does.Contain("disk error"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldApplyTokenReplacementWhenTokensProvided()
    {
        var sqlFile = "test_token.sql";
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(sqlFile).Returns(true);
        mockFileWrapper.ReadAllText(sqlFile).Returns("USE [{{MainDB}}]\nGO\nSELECT '{{TemplateName}}'");
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var tokens = new List<KeyValuePair<string, string>>
            {
                new("MainDB", "Production"),
                new("TemplateName", "MyTemplate")
            };
            var script = SqlScript.Load(sqlFile, tokens);
            Assert.Multiple(() =>
            {
                Assert.That(script.Batches[0], Does.Contain("USE [Production]"));
                Assert.That(script.Batches[1], Does.Contain("SELECT 'MyTemplate'"));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldNotApplyTokensWhenNullProvided()
    {
        var sqlFile = "test_no_token.sql";
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(sqlFile).Returns(true);
        mockFileWrapper.ReadAllText(sqlFile).Returns("USE [{{MainDB}}]");
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var script = SqlScript.Load(sqlFile);
            Assert.That(script.Batches[0], Does.Contain("{{MainDB}}"));

            FactoryContainer.Clear();
        }
    }
}

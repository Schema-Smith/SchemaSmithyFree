// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using log4net;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class LogBackupTests
{
    private IEnvironment _mockEnvironment;
    private IDirectory _mockDirectory;
    private IFile _mockFile;

    [SetUp]
    public void SetUp()
    {
        _mockEnvironment = Substitute.For<IEnvironment>();
        _mockDirectory = Substitute.For<IDirectory>();
        _mockFile = Substitute.For<IFile>();

        FactoryContainer.Register<IEnvironment>(_mockEnvironment);
        FactoryContainer.Register<IDirectory>(_mockDirectory);
        FactoryContainer.Register<IFile>(_mockFile);

        // Set default command line (no LogPath switch)
        _mockEnvironment.CommandLine.Returns("app.exe");

        LogFactory.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
        LogFactory.Clear();
    }

    [Test]
    public void BackupLogsAndExit_CreatesBackupDirectory()
    {
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());

        LogBackup.BackupLogsAndExit("TestApp");

        _mockDirectory.Received().CreateDirectory(Arg.Is<string>(s => s.Contains("TestApp.0001")));
    }

    [Test]
    public void BackupLogsAndExit_IncrementsDirectoryWhenExists()
    {
        _mockDirectory.Exists(Arg.Is<string>(s => s.Contains("0001"))).Returns(true);
        _mockDirectory.Exists(Arg.Is<string>(s => s.Contains("0002"))).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());

        LogBackup.BackupLogsAndExit("TestApp");

        _mockDirectory.Received().CreateDirectory(Arg.Is<string>(s => s.Contains("TestApp.0002")));
    }

    [Test]
    public void BackupLogsAndExit_CopiesLogFiles()
    {
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), "TestApp - *.log", SearchOption.TopDirectoryOnly)
            .Returns(new[] { "/logs/TestApp - Progress.log", "/logs/TestApp - Error.log" });

        LogBackup.BackupLogsAndExit("TestApp");

        _mockFile.Received(2).Copy(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public void BackupLogsAndExit_ExitsWithSpecifiedCode()
    {
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());

        LogBackup.BackupLogsAndExit("TestApp", 2);

        _mockEnvironment.Received().Exit(2);
    }

    [Test]
    public void BackupLogsAndExit_ExitsWithZeroByDefault()
    {
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());

        LogBackup.BackupLogsAndExit("TestApp");

        _mockEnvironment.Received().Exit(0);
    }

    [Test]
    public void BackupLogsAndExit_ExitsWithCode4OnException()
    {
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.CreateDirectory(Arg.Any<string>()).Returns(_ => throw new Exception("disk full"));

        LogBackup.BackupLogsAndExit("TestApp");

        _mockEnvironment.Received().Exit(4);
    }

    [Test]
    public void BackupLogsAndExit_UsesLogPathSwitch()
    {
        var expectedBase = Path.GetFullPath("/custom/logs").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _mockEnvironment.CommandLine.Returns("app.exe --LogPath:/custom/logs");
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());

        LogBackup.BackupLogsAndExit("TestApp");

        _mockDirectory.Received().CreateDirectory(Arg.Is<string>(s =>
            s.Contains(expectedBase) && s.Contains("TestApp.0001")));
    }

    [Test]
    public void BackupLogsAndExit_UsesResolvedLogPath()
    {
        var expectedBase = Path.GetFullPath("relbackup").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _mockEnvironment.CommandLine.Returns("app.exe --LogPath:relbackup");
        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());

        LogBackup.BackupLogsAndExit("TestApp");

        _mockDirectory.Received().CreateDirectory(Arg.Is<string>(s =>
            s == Path.Combine(expectedBase, "TestApp.0001")));
        _mockDirectory.Received().GetFiles(expectedBase, "TestApp - *.log", SearchOption.TopDirectoryOnly);
    }

    [Test]
    public void UnhandledExceptionLogger_LogsToProgressAndErrorLogs()
    {
        var mockProgressLog = Substitute.For<ILog>();
        var mockErrorLog = Substitute.For<ILog>();
        LogFactory.Register("ProgressLog", mockProgressLog);
        LogFactory.Register("ErrorLog", mockErrorLog);

        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());

        var exception = new Exception("test error");
        var args = new UnhandledExceptionEventArgs(exception, false);

        LogBackup.UnhandledExceptionLogger("TestApp", args);

        mockProgressLog.Received().Error(Arg.Is<string>(s => s.Contains("EXCEPTION")));
        mockErrorLog.Received().Error(exception);
    }

    [Test]
    public void UnhandledExceptionLogger_ExitsWithCode3()
    {
        var mockProgressLog = Substitute.For<ILog>();
        var mockErrorLog = Substitute.For<ILog>();
        LogFactory.Register("ProgressLog", mockProgressLog);
        LogFactory.Register("ErrorLog", mockErrorLog);

        _mockDirectory.Exists(Arg.Any<string>()).Returns(false);
        _mockDirectory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(Array.Empty<string>());

        var args = new UnhandledExceptionEventArgs(new Exception("test"), false);

        LogBackup.UnhandledExceptionLogger("TestApp", args);

        _mockEnvironment.Received().Exit(3);
    }
}

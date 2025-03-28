using log4net;
using Schema.Isolators;
using Schema.Utility;
using NSubstitute;
using System;
using System.IO;

namespace Schema.UnitTests;

public class LogBackupTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();

    [Test]
    public void ShouldLogUnhandledExceptions()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(_environment);
            LogFactory.Register("ErrorLog", _errorLog);
            LogFactory.Register("ProgressLog", _progressLog);

            var exception = new Exception("Test Exception");
            LogBackup.UnhandledExceptionLogger("TestApp", new UnhandledExceptionEventArgs(exception, false));

            _progressLog.Received(1).Error("EXCEPTION - See the error log:\r\nSystem.Exception: Test Exception");
            _errorLog.Received(1).Error(exception);
            _environment.Received(1).Exit(3);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldBackupLogsAndExit()
    {
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(false);
        mockDirectoryWrapper.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(new[] { "TestApp - Progress.log" });
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(_environment);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);
            LogFactory.Register("ErrorLog", _errorLog);
            LogFactory.Register("ProgressLog", _progressLog);

            LogBackup.BackupLogsAndExit("TestApp", 1);

            mockDirectoryWrapper.Received(1).CreateDirectory(Arg.Is<string>(s => s.EndsWith("TestApp.0001")));
            mockDirectoryWrapper.Received(1).GetFiles(Arg.Any<string>(), "TestApp - *.log", SearchOption.TopDirectoryOnly);
            _environment.Received(1).Exit(1);
            mockFileWrapper.Received(1).Copy(Arg.Is<string>(s => s.EndsWith("TestApp - Progress.log")), Arg.Is<string>(s => s.EndsWith(Path.Combine("TestApp.0001", "TestApp - Progress.log"))));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldHandleErrorBackingUpFile()
    {
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(false);
        mockDirectoryWrapper.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(new[] { "TestApp - Progress.log" });
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);
        mockFileWrapper.When(f => f.Copy(Arg.Any<string>(), Arg.Any<string>())).Do(x => throw new Exception("Test Exception"));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(_environment);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);
            LogFactory.Register("ErrorLog", _errorLog);
            LogFactory.Register("ProgressLog", _progressLog);

            LogBackup.BackupLogsAndExit("TestApp", 1);

            _environment.Received(1).Exit(4);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}

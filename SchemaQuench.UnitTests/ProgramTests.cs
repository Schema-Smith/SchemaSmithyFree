using System;
using log4net;
using Schema.Isolators;
using Schema.Utility;
using NSubstitute;

namespace SchemaQuench.UnitTests;

public class ProgramTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();

    [Test]
    public void ShouldHandleUnhandledException()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(_environment);
            LogFactory.Register("ErrorLog", _errorLog);
            LogFactory.Register("ProgressLog", _progressLog);

            var exception = new Exception("Test Exception");
            Program.UnhandledException("TestApp", new UnhandledExceptionEventArgs(exception, false));

            _progressLog.Received(1).Error("EXCEPTION - See the error log:\r\nSystem.Exception: Test Exception");
            _errorLog.Received(1).Error(exception);
            _environment.Received(1).Exit(3);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}

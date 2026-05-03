// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NSubstitute;
using Schema.Isolators;

namespace Schema.IntegrationTests;

/// <summary>
/// Shared factory for IEnvironment test mocks.
///
/// Use <see cref="CreateStrict"/> when writing new tests that exercise code paths which
/// call <see cref="IEnvironment.Exit"/> on failure. A plain <c>Substitute.For&lt;IEnvironment&gt;()</c>
/// treats <c>Exit(nonZero)</c> as a no-op method call, which lets production code that uses
/// <c>LogBackup.BackupLogsAndExit(exitCode)</c> continue executing past the intended terminate
/// point. Any state those continuations write (checkpoints, tracking entries, etc.) can
/// silently mask a real failure and cause flaky tests or wrong test expectations.
///
/// <see cref="CreateStrict"/> throws <see cref="TestProcessExitException"/> from any non-zero
/// Exit call so the test harness fails loudly. Tests using the strict mock must wrap the code
/// under test in <c>Assert.Throws&lt;TestProcessExitException&gt;(...)</c> when a non-zero exit
/// is the expected outcome.
/// </summary>
public static class TestEnvironment
{
    /// <summary>
    /// Creates an IEnvironment mock that throws <see cref="TestProcessExitException"/>
    /// on any non-zero <see cref="IEnvironment.Exit"/> call. Zero-code exits (clean
    /// termination paths) are recorded normally.
    /// </summary>
    public static IEnvironment CreateStrict()
    {
        var env = Substitute.For<IEnvironment>();
        env.When(x => x.Exit(Arg.Is<int>(code => code != 0)))
           .Do(call => throw new TestProcessExitException((int)call[0]));
        return env;
    }
}

/// <summary>
/// Thrown from a strict IEnvironment test mock when production code invokes
/// <c>Exit(nonZero)</c>. Simulates the process-termination semantics of the real
/// <c>Environment.Exit</c> call inside the test process, preventing code paths that
/// would never run in production (because the process would already be gone) from
/// executing in tests.
/// </summary>
public class TestProcessExitException : Exception
{
    public int ExitCode { get; }

    public TestProcessExitException(int exitCode)
        : base($"IEnvironment.Exit({exitCode}) invoked during test — would have terminated the process in production.")
    {
        ExitCode = exitCode;
    }
}

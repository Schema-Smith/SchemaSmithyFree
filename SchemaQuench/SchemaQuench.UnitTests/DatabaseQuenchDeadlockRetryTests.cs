// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;
using Schema.Isolators;

namespace SchemaQuench.UnitTests;

/// <summary>
/// Covers the proc-level deadlock-retry behaviour at the ExecuteNonQueryHandlingMessages
/// chokepoint (Rule 15 — tested at the layer the capability lives, not a call site). Retry is
/// opt-in and only the idempotent convergence procs request it.
/// </summary>
[TestFixture]
public class DatabaseQuenchDeadlockRetryTests
{
    [TearDown]
    public void TearDown() => FactoryContainer.Clear();

    private static DatabaseQuench NewQuench()
    {
        var product = new Product { Name = "P", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", false, false, false, null);
        quench.DeadlockRetryBaseMs = 0;   // no real sleeping in unit tests
        quench.MaxDeadlockAttempts = 4;
        return quench;
    }

    /// <summary>A command whose ExecuteNonQuery throws the given exception for the first
    /// <paramref name="failTimes"/> calls, then succeeds. Tracks the call count.</summary>
    private static IDbCommand CommandFailing(int failTimes, Func<Exception> error, out Func<int> callCount)
    {
        var cmd = Substitute.For<IDbCommand>();
        var calls = 0;
        cmd.When(c => c.ExecuteNonQuery()).Do(_ =>
        {
            calls++;
            if (calls <= failTimes) throw error();
        });
        callCount = () => calls;
        return cmd;
    }

    [Test]
    public void RetryEnabled_DeadlockThenSuccess_RetriesUntilSuccess()
    {
        var quench = NewQuench();
        var cmd = CommandFailing(2, () => new SqlServerErrorException(1205, "deadlock victim"), out var calls);

        Assert.DoesNotThrow(() => quench.ExecuteNonQueryHandlingMessages(cmd, retryOnDeadlock: true));
        Assert.That(calls(), Is.EqualTo(3), "should retry twice then succeed on the third attempt");
    }

    [Test]
    public void RetryEnabled_PersistentDeadlock_GivesUpAfterMaxAttempts()
    {
        var quench = NewQuench();
        var cmd = CommandFailing(int.MaxValue, () => new SqlServerErrorException(1205, "deadlock victim"), out var calls);

        Assert.Throws<SqlServerErrorException>(
            () => quench.ExecuteNonQueryHandlingMessages(cmd, retryOnDeadlock: true));
        Assert.That(calls(), Is.EqualTo(4), "should attempt exactly MaxDeadlockAttempts times");
    }

    [Test]
    public void RetryEnabled_NonDeadlockError_ThrowsImmediately()
    {
        var quench = NewQuench();
        var cmd = CommandFailing(int.MaxValue, () => new Exception("syntax error near SELCT"), out var calls);

        Assert.Throws<Exception>(
            () => quench.ExecuteNonQueryHandlingMessages(cmd, retryOnDeadlock: true));
        Assert.That(calls(), Is.EqualTo(1), "non-deadlock errors must not be retried");
    }

    [Test]
    public void RetryDisabled_Deadlock_ThrowsImmediately()
    {
        var quench = NewQuench();
        var cmd = CommandFailing(int.MaxValue, () => new SqlServerErrorException(1205, "deadlock victim"), out var calls);

        Assert.Throws<SqlServerErrorException>(
            () => quench.ExecuteNonQueryHandlingMessages(cmd, retryOnDeadlock: false));
        Assert.That(calls(), Is.EqualTo(1), "retry must be opt-in; default path does not retry");
    }
}

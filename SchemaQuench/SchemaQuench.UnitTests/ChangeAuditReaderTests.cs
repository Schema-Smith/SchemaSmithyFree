// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;

namespace SchemaQuench.UnitTests;

/// <summary>
/// Covers the end-of-work-unit ChangeAudit drain's best-effort contract at the
/// <see cref="ChangeAuditReader"/> boundary (Rule 15). The drain runs in the quench's
/// <c>finally</c> on the shared table connection; its documented contract is that an audit-read
/// failure must NEVER disrupt the run. A broken/closed connection surfaces as
/// <see cref="InvalidOperationException"/> ("Connection is not open") — not a
/// <see cref="DbException"/> — so the swallow must be broad enough to cover it, otherwise the
/// drain masks the real result and fails an otherwise-successful quench under concurrency.
/// </summary>
[TestFixture]
public class ChangeAuditReaderTests
{
    private static IDbCommand CommandWhoseReaderThrows(Exception error)
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.When(c => c.ExecuteReader()).Do(_ => throw error);
        return cmd;
    }

    [TestCase(Platform.PostgreSQL)]
    [TestCase(Platform.SqlServer)]
    [TestCase(Platform.MySQL)]
    [TestCase(Platform.MariaDb)]
    public void ReadAndDrain_BrokenConnection_ReturnsNullInsteadOfThrowing(Platform platform)
    {
        // "Connection is not open" from a connection reset under concurrency (Npgsql throws
        // InvalidOperationException, not a DbException). The best-effort drain must swallow it.
        var cmd = CommandWhoseReaderThrows(new InvalidOperationException("Connection is not open"));

        IReadOnlyList<ChangeAuditRow> rows = null;
        Assert.DoesNotThrow(() => rows = ChangeAuditReader.ReadAndDrain(platform, cmd));
        Assert.That(rows, Is.Null, "A broken connection during the best-effort drain must leave the run not-instrumented, not throw.");
    }

    [Test]
    public void ReadAndDrain_MissingAuditTable_ReturnsNull()
    {
        // Regression guard on the pre-existing DbException path (audit table absent / kindling
        // suppressed) — must still return null.
        var cmd = CommandWhoseReaderThrows(new TestDbException("relation \"SchemaSmith\".\"ChangeAudit\" does not exist"));

        IReadOnlyList<ChangeAuditRow> rows = null;
        Assert.DoesNotThrow(() => rows = ChangeAuditReader.ReadAndDrain(Platform.PostgreSQL, cmd));
        Assert.That(rows, Is.Null);
    }

    private sealed class TestDbException : DbException
    {
        public TestDbException(string message) : base(message) { }
    }
}

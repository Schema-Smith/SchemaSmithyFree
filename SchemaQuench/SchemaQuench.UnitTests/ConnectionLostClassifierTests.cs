// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Net.Sockets;
using Npgsql;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class ConnectionLostClassifierTests
{
    // --- Positive: the server went away mid-deploy ---

    [Test] // the literal #353 surface: a provider error wrapping an inner SocketException
    public void InnerSocketException_IsConnectionLost()
    {
        var ex = new Exception("A network-related or instance-specific error occurred",
            new SocketException(0));
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.True);
    }

    [Test] // SQL Server tears the session down mid-command before the socket error surfaces
    public void KillStateMessage_IsConnectionLost()
    {
        var ex = new Exception("Cannot continue the execution because the session is in the kill state.");
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.True);
    }

    [Test] // PostgreSQL server-initiated shutdown
    public void PostgresAdminShutdown_57P01_IsConnectionLost()
    {
        var ex = new PostgresException("terminating connection due to administrator command",
            "FATAL", "FATAL", "57P01");
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.True);
    }

    [Test] // PostgreSQL connection-exception class (08*)
    public void PostgresConnectionClass_08006_IsConnectionLost()
    {
        var ex = new PostgresException("connection failure", "FATAL", "FATAL", "08006");
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.True);
    }

    [Test] // PG/MySQL transport drop surfaces as a provider exception wrapping IOException
    public void InnerIoException_IsConnectionLost()
    {
        var ex = new Exception("Exception while reading from stream", new IOException("Unable to read data"));
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.True);
    }

    // --- Negative: NOT a connection loss (schema/script errors, contention) ---

    [Test]
    public void OrdinaryError_IsNotConnectionLost()
    {
        var ex = new Exception("Invalid column name 'Widget'.");
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.False);
    }

    [Test] // a real server-returned error (undefined table) is a schema error, not a drop
    public void PostgresUndefinedTable_42P01_IsNotConnectionLost()
    {
        var ex = new PostgresException("relation \"x\" does not exist", "ERROR", "ERROR", "42P01");
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.False);
    }

    [Test] // a deadlock is retryable contention (DeadlockClassifier's job), not a connection loss
    public void Deadlock_IsNotConnectionLost()
    {
        var ex = new SqlServerErrorException(1205, "Transaction was deadlocked on lock resources");
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.False);
    }

    [Test] // walks the inner chain to find the drop
    public void NestedInnerSocketException_IsConnectionLost()
    {
        var ex = new Exception("outer",
            new Exception("Error occurred while kindling 'X.sql'.",
                new SocketException(10054)));
        Assert.That(ConnectionLostClassifier.IsConnectionLost(ex), Is.True);
    }
}

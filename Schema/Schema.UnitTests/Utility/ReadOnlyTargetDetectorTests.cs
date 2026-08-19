// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NSubstitute;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class ReadOnlyTargetDetectorTests
{
    private static IDbCommand CommandReturning(object scalar)
    {
        var command = Substitute.For<IDbCommand>();
        command.ExecuteScalar().Returns(scalar);
        return command;
    }

    [TestCase(Platform.SqlServer)]
    [TestCase(Platform.PostgreSQL)]
    [TestCase(Platform.MySQL)]
    [TestCase(Platform.MariaDb)]
    public void IsReadOnly_One_IsTrue(Platform platform)
    {
        Assert.That(ReadOnlyTargetDetector.IsReadOnly(CommandReturning(1), platform), Is.True);
    }

    [TestCase(Platform.SqlServer)]
    [TestCase(Platform.PostgreSQL)]
    [TestCase(Platform.MySQL)]
    [TestCase(Platform.MariaDb)]
    public void IsReadOnly_Zero_IsFalse(Platform platform)
    {
        Assert.That(ReadOnlyTargetDetector.IsReadOnly(CommandReturning(0), platform), Is.False);
    }

    [Test]
    public void IsReadOnly_NullOrDbNull_IsFalse()
    {
        Assert.That(ReadOnlyTargetDetector.IsReadOnly(CommandReturning(null), Platform.SqlServer), Is.False);
        Assert.That(ReadOnlyTargetDetector.IsReadOnly(CommandReturning(DBNull.Value), Platform.SqlServer), Is.False);
    }

    [Test]
    public void IsReadOnly_StringScalar_IsConverted()
    {
        // PostgreSQL's driver can hand back the CASE result as a string depending on the type map.
        Assert.That(ReadOnlyTargetDetector.IsReadOnly(CommandReturning("1"), Platform.PostgreSQL), Is.True);
        Assert.That(ReadOnlyTargetDetector.IsReadOnly(CommandReturning("0"), Platform.PostgreSQL), Is.False);
    }

    [Test]
    public void GetReadOnlyQuery_SqlServer_ChecksUpdateability()
    {
        Assert.That(ReadOnlyTargetDetector.GetReadOnlyQuery(Platform.SqlServer), Does.Contain("DATABASEPROPERTYEX"));
        Assert.That(ReadOnlyTargetDetector.GetReadOnlyQuery(Platform.SqlServer), Does.Contain("READ_ONLY"));
    }

    [Test]
    public void GetReadOnlyQuery_PostgreSql_ChecksRecoveryAndTransactionReadOnly()
    {
        var query = ReadOnlyTargetDetector.GetReadOnlyQuery(Platform.PostgreSQL);

        Assert.That(query, Does.Contain("pg_is_in_recovery"));
        Assert.That(query, Does.Contain("transaction_read_only"));
    }

    [Test]
    public void GetReadOnlyQuery_MariaDb_DoesNotReferenceSuperReadOnly()
    {
        // super_read_only does not exist on MariaDB — referencing it raises "Unknown system
        // variable" and fails the whole check, so MariaDB must read @@read_only alone.
        var query = ReadOnlyTargetDetector.GetReadOnlyQuery(Platform.MariaDb);

        Assert.That(query, Does.Contain("@@read_only"));
        Assert.That(query, Does.Not.Contain("super_read_only"));
    }

    [Test]
    public void GetReadOnlyQuery_MySql_ChecksSuperReadOnly()
    {
        Assert.That(ReadOnlyTargetDetector.GetReadOnlyQuery(Platform.MySQL), Does.Contain("super_read_only"));
    }

    [Test]
    public void GetReadOnlyQuery_UnsupportedPlatform_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadOnlyTargetDetector.GetReadOnlyQuery((Platform)999));
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;

namespace SchemaQuench.UnitTests;

/// <summary>
/// Slice-3 (#257) unit tests for <see cref="SchemaProvisioner"/>. The provisioner emits per-engine
/// idempotent CREATE SCHEMA DDL (design §6); existence checking and the broader skip-missing flow
/// live in <see cref="DatabaseQuench"/>. Tests assert on the SQL text + WhatIf behavior + the
/// MySQL-not-supported guard.
/// </summary>
[TestFixture]
public class SchemaProvisionerTests
{
    [Test]
    public void EnsureSchemaExists_SqlServer_IssuesCreateIfNotExists()
    {
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureSchemaExists(command, "new_tenant", Platform.SqlServer, isWhatIf: false, log: _ => { });

        Assert.That(executed, Has.Count.EqualTo(1));
        Assert.That(executed[0], Does.Contain("IF NOT EXISTS")
            .And.Contain("sys.schemas")
            .And.Contain("CREATE SCHEMA [new_tenant]"));
    }

    [Test]
    public void EnsureSchemaExists_PostgreSQL_IssuesCreateSchemaIfNotExists()
    {
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureSchemaExists(command, "new_tenant", Platform.PostgreSQL, isWhatIf: false, log: _ => { });

        Assert.That(executed, Has.Count.EqualTo(1));
        Assert.That(executed[0], Is.EqualTo("CREATE SCHEMA IF NOT EXISTS \"new_tenant\""));
    }

    [Test]
    public void EnsureSchemaExists_MySQL_Throws_SchemaAxisNotSupported()
    {
        var provisioner = new SchemaProvisioner();
        var command = Substitute.For<IDbCommand>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            provisioner.EnsureSchemaExists(command, "x", Platform.MySQL, isWhatIf: false, log: _ => { }));

        Assert.That(ex!.Message, Does.Contain("MySQL")
            .And.Contain("EnsureDatabaseExists"));
    }

    [Test]
    public void EnsureSchemaExists_WhatIf_DoesNotExecuteAndLogsWouldCreate()
    {
        // WhatIf must NOT touch the database. Provisioning DDL renders through the log
        // surface using the existing "[WhatIf] Would <verb>" convention so the rest of the
        // engine's WhatIf summary continues to reflect what a real run would do.
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);
        var logged = new List<string>();

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureSchemaExists(command, "new_tenant", Platform.SqlServer, isWhatIf: true,
            log: logged.Add);

        Assert.Multiple(() =>
        {
            Assert.That(executed, Is.Empty, "WhatIf must not execute provisioning DDL.");
            Assert.That(logged, Has.Some.Matches<string>(s =>
                s.Contains("[WhatIf]") && s.Contains("Would create schema") && s.Contains("new_tenant")));
        });
    }

    [Test]
    public void EnsureSchemaExists_SqlServer_EscapesEmbeddedBrackets()
    {
        // ] inside a schema name is invalid per SchemaDiscovery's validation guard, but the
        // provisioner does its own escape so a future caller bypassing the guard still produces
        // safely-escaped DDL. Belt-and-suspenders for SQL identifier quoting.
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureSchemaExists(command, "weird]name", Platform.SqlServer, isWhatIf: false, log: _ => { });

        Assert.That(executed[0], Does.Contain("CREATE SCHEMA [weird]]name]"));
    }

    [Test]
    public void EnsureSchemaExists_PostgreSQL_EscapesEmbeddedQuotes()
    {
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureSchemaExists(command, "weird\"name", Platform.PostgreSQL, isWhatIf: false, log: _ => { });

        Assert.That(executed[0], Is.EqualTo("CREATE SCHEMA IF NOT EXISTS \"weird\"\"name\""));
    }

    private static IDbCommand MakeRecordingCommand(List<string> executedTexts)
    {
        var command = Substitute.For<IDbCommand>();
        command.When(c => c.ExecuteNonQuery())
            .Do(_ => executedTexts.Add(command.CommandText));
        return command;
    }
}

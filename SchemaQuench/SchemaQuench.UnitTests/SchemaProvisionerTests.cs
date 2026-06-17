// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;

namespace SchemaQuench.UnitTests;

/// <summary>
/// Unit tests for <see cref="SchemaProvisioner"/>. The provisioner emits per-engine idempotent
/// CREATE SCHEMA DDL; existence checking and the broader skip-missing flow live in
/// <see cref="DatabaseQuench"/>. Tests assert on the SQL text + WhatIf behavior + the
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

    // ------- Slice 4 (#257) database-axis provisioning -----------------------------------------

    [Test]
    public void EnsureDatabaseExists_SqlServer_IssuesIfNotExistsCreateDatabase()
    {
        // SQL Server supports `IF NOT EXISTS (SELECT 1 FROM sys.databases ...) CREATE DATABASE [..]`
        // as a single statement — same idempotent gate pattern as the schema-axis path. The caller
        // is responsible for opening the admin command against `master`; the provisioner only
        // emits the DDL on the supplied command.
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "tenant_acme", Platform.SqlServer, isWhatIf: false, log: _ => { });

        Assert.That(executed, Has.Count.EqualTo(1));
        Assert.That(executed[0], Does.Contain("IF NOT EXISTS")
            .And.Contain("sys.databases")
            .And.Contain("CREATE DATABASE [tenant_acme]"));
    }

    [Test]
    public void EnsureDatabaseExists_PostgreSQL_ChecksPgDatabaseAndCreates()
    {
        // PostgreSQL has no `CREATE DATABASE IF NOT EXISTS` — the provisioner checks pg_database
        // explicitly via ExecuteScalar, then runs `CREATE DATABASE "name"` only if the count is 0.
        // Stub ExecuteScalar to return 0 (missing) so the create fires.
        var executedNonQuery = new List<string>();
        var executedScalar = new List<string>();
        var command = Substitute.For<IDbCommand>();
        command.When(c => c.ExecuteNonQuery())
            .Do(_ => executedNonQuery.Add(command.CommandText));
        // ExecuteScalar both records the SQL text AND returns 0 so the provisioner proceeds to
        // CREATE. NSubstitute can't combine When().Do() with Returns() on the same call, so wire
        // the recording side-effect through the Returns callback.
        command.ExecuteScalar().Returns(_ =>
        {
            executedScalar.Add(command.CommandText);
            return (object)0L;
        });

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "tenant_acme", Platform.PostgreSQL, isWhatIf: false, log: _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(executedScalar, Has.Count.EqualTo(1));
            Assert.That(executedScalar[0], Does.Contain("pg_database")
                .And.Contain("datname")
                .And.Contain("'tenant_acme'"));
            Assert.That(executedNonQuery, Has.Count.EqualTo(1));
            Assert.That(executedNonQuery[0], Is.EqualTo("CREATE DATABASE \"tenant_acme\""));
        });
    }

    [Test]
    public void EnsureDatabaseExists_PostgreSQL_AlreadyExists_DoesNotIssueCreate()
    {
        // Belt-and-suspenders idempotence: if pg_database returns a count > 0, the provisioner
        // must NOT issue CREATE DATABASE. The check-then-create gate runs on the caller's command.
        var executedNonQuery = new List<string>();
        var command = Substitute.For<IDbCommand>();
        command.When(c => c.ExecuteNonQuery())
            .Do(_ => executedNonQuery.Add(command.CommandText));
        command.ExecuteScalar().Returns(_ => (object)1L);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "tenant_acme", Platform.PostgreSQL, isWhatIf: false, log: _ => { });

        Assert.That(executedNonQuery, Is.Empty,
            "PG provisioner must not CREATE when pg_database count > 0.");
    }

    [Test]
    public void EnsureDatabaseExists_MySQL_IssuesCreateIfNotExists()
    {
        // MySQL has native `CREATE DATABASE IF NOT EXISTS` — single-statement idempotent path.
        // MySQL also has no schema/database distinction, so the database axis is the only
        // provisioning surface (the schema-axis path throws on MySQL — slice 3).
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "tenant_acme", Platform.MySQL, isWhatIf: false, log: _ => { });

        Assert.That(executed, Has.Count.EqualTo(1));
        Assert.That(executed[0], Is.EqualTo("CREATE DATABASE IF NOT EXISTS `tenant_acme`"));
    }

    [Test]
    public void EnsureDatabaseExists_WhatIf_DoesNotExecuteAndLogsWouldCreate()
    {
        // Mirror the schema-axis WhatIf contract: under WhatIf the DDL must NOT touch the
        // database; the action renders through the log surface using the engine's existing
        // "[WhatIf] Would <verb>" convention so the WhatIf summary accurately reflects what a
        // real run would do.
        var executedNonQuery = new List<string>();
        var executedScalar = new List<string>();
        var command = Substitute.For<IDbCommand>();
        command.When(c => c.ExecuteNonQuery())
            .Do(_ => executedNonQuery.Add(command.CommandText));
        command.ExecuteScalar().Returns(_ =>
        {
            executedScalar.Add(command.CommandText);
            return (object)0L;
        });
        var logged = new List<string>();

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "tenant_acme", Platform.SqlServer, isWhatIf: true,
            log: logged.Add);

        Assert.Multiple(() =>
        {
            Assert.That(executedNonQuery, Is.Empty, "WhatIf must not execute provisioning DDL.");
            Assert.That(executedScalar, Is.Empty, "WhatIf must not execute existence checks either.");
            Assert.That(logged, Has.Some.Matches<string>(s =>
                s.Contains("[WhatIf]") && s.Contains("Would create database") && s.Contains("tenant_acme")));
        });
    }

    [Test]
    public void EnsureDatabaseExists_SqlServer_EscapesEmbeddedBrackets()
    {
        // ] inside a database name is rejected by upstream validation, but the provisioner does
        // its own escape so a future caller bypassing the guard still produces safely-escaped DDL.
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "weird]name", Platform.SqlServer, isWhatIf: false, log: _ => { });

        Assert.That(executed[0], Does.Contain("CREATE DATABASE [weird]]name]")
            .And.Contain("name = 'weird]name'"));
    }

    [Test]
    public void EnsureDatabaseExists_PostgreSQL_EscapesEmbeddedQuotes()
    {
        var command = Substitute.For<IDbCommand>();
        command.ExecuteScalar().Returns(_ => (object)0L);
        var executed = new List<string>();
        command.When(c => c.ExecuteNonQuery())
            .Do(_ => executed.Add(command.CommandText));

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "weird\"name", Platform.PostgreSQL, isWhatIf: false, log: _ => { });

        Assert.That(executed[0], Is.EqualTo("CREATE DATABASE \"weird\"\"name\""));
    }

    [Test]
    public void EnsureDatabaseExists_MySQL_EscapesEmbeddedBackticks()
    {
        var executed = new List<string>();
        var command = MakeRecordingCommand(executed);

        var provisioner = new SchemaProvisioner();
        provisioner.EnsureDatabaseExists(command, "weird`name", Platform.MySQL, isWhatIf: false, log: _ => { });

        Assert.That(executed[0], Is.EqualTo("CREATE DATABASE IF NOT EXISTS `weird``name`"));
    }

    [Test]
    public void EnsureDatabaseExists_PostgreSQL_RaceLoser_ReturnsWithoutThrowing()
    {
        // Under concurrent parallel quenches the existence check + CREATE DATABASE on PG is a
        // check-then-act race. The losing concurrent quench sees pg_database COUNT(*) = 0 from
        // its check, then hits SqlState 42P04 ("database already exists") when its CREATE runs
        // after the race-winner's CREATE commits. The race-loser path is BENIGN — the database
        // exists; the idempotency contract is preserved — and must NOT re-wrap as a misleading
        // "must have CREATE DATABASE permission" diagnostic via the generic outer catch.
        var executedScalar = new List<string>();
        var executedNonQuery = new List<string>();
        var command = Substitute.For<IDbCommand>();
        // ExecuteScalar returns 0 (DB doesn't exist when WE check) so the CREATE branch fires.
        command.ExecuteScalar().Returns(_ =>
        {
            executedScalar.Add(command.CommandText);
            return (object)0L;
        });
        // The CREATE throws SqlState 42P04 (race-loser).
        var raceLoserEx = new PostgresException(
            "database \"tenant_acme\" already exists", "ERROR", "ERROR", "42P04");
        command.When(c => c.ExecuteNonQuery())
            .Do(_ =>
            {
                executedNonQuery.Add(command.CommandText);
                throw raceLoserEx;
            });

        var provisioner = new SchemaProvisioner();
        // Must NOT throw — the race-loser is treated as a successful idempotent return.
        Assert.DoesNotThrow(() =>
            provisioner.EnsureDatabaseExists(command, "tenant_acme", Platform.PostgreSQL, isWhatIf: false,
                log: _ => { }));

        Assert.Multiple(() =>
        {
            // The CREATE DATABASE was attempted (we're testing the race-loser, not the
            // already-exists short-circuit).
            Assert.That(executedNonQuery, Has.Some.EqualTo("CREATE DATABASE \"tenant_acme\""),
                "The race-loser test must exercise the CREATE path that throws 42P04, not the " +
                "pre-existence short-circuit (which would never hit the new catch at all).");
        });
    }

    [Test]
    public void EnsureDatabaseExists_EnginePermissionFailure_WrapsWithActionableDiagnostic()
    {
        // CREATE DATABASE is a high-privilege operation and a permission denial is the most
        // likely runtime failure mode. The provisioner wraps any engine exception in a
        // TemplateTargetProvisioningException carrying an actionable diagnostic naming the
        // target DB + the required privilege, with the engine exception preserved as
        // InnerException so the root cause isn't lost.
        var command = Substitute.For<IDbCommand>();
        var underlying = new InvalidOperationException("CREATE DATABASE permission denied in database 'master'.");
        command.When(c => c.ExecuteNonQuery()).Do(_ => throw underlying);

        var provisioner = new SchemaProvisioner();
        var ex = Assert.Throws<TemplateTargetProvisioningException>(() =>
            provisioner.EnsureDatabaseExists(command, "tenant_acme", Platform.SqlServer, isWhatIf: false,
                log: _ => { }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("tenant_acme")
                .And.Contain("CREATE DATABASE permission"));
            Assert.That(ex.InnerException, Is.SameAs(underlying),
                "Original engine exception must be preserved as InnerException so the underlying " +
                "failure isn't lost to the diagnostic.");
        });
    }

    private static IDbCommand MakeRecordingCommand(List<string> executedTexts)
    {
        var command = Substitute.For<IDbCommand>();
        command.When(c => c.ExecuteNonQuery())
            .Do(_ => executedTexts.Add(command.CommandText));
        return command;
    }
}

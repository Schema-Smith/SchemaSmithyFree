// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class SchemaDiscoveryTests
{
    private static Template SqlServerSchemaTemplate(string script = "SELECT name FROM sys.schemas")
    {
        var product = new Product { Name = "P", Platform = Platform.SqlServer };
        return new Template
        {
            Name = "TenantBody",
            Product = product,
            SchemaIdentificationScript = script
        };
    }

    private static Template PostgreSqlSchemaTemplate(string script = "SELECT nspname FROM pg_namespace")
    {
        var product = new Product { Name = "P", Platform = Platform.PostgreSQL };
        return new Template
        {
            Name = "TenantBody",
            Product = product,
            SchemaIdentificationScript = script
        };
    }

    private static Template MySqlTemplate()
    {
        var product = new Product { Name = "P", Platform = Platform.MySQL };
        return new Template
        {
            Name = "TenantBody",
            Product = product
        };
    }

    private static IDbCommand MockCommandReturning(params object[] rows)
    {
        var cmd = Substitute.For<IDbCommand>();
        var reader = Substitute.For<IDataReader>();
        cmd.ExecuteReader().Returns(reader);
        reader.FieldCount.Returns(1);

        // Configure Read() to return true for each row then false.
        var readReturns = new bool[rows.Length + 1];
        for (var i = 0; i < rows.Length; i++) readReturns[i] = true;
        readReturns[rows.Length] = false;
        if (rows.Length == 0)
        {
            reader.Read().Returns(false);
        }
        else if (rows.Length == 1)
        {
            reader.Read().Returns(true, false);
            reader[0].Returns(rows[0]);
        }
        else
        {
            // NSubstitute Returns with params: first call returns first arg, second returns second, etc.
            reader.Read().Returns(readReturns[0], readReturns.Skip(1).ToArray());
            reader[0].Returns(rows[0], rows.Skip(1).ToArray());
        }

        return cmd;
    }

    [Test]
    public void Discover_SqlServer_HappyPath_ReturnsSchemaNames()
    {
        var template = SqlServerSchemaTemplate();
        var cmd = MockCommandReturning("tenant_a", "tenant_b");

        var result = SchemaDiscovery.Discover(cmd, template);

        Assert.That(result, Is.EqualTo(new List<string> { "tenant_a", "tenant_b" }));
        Assert.That(cmd.CommandText, Is.EqualTo(template.SchemaIdentificationScript));
    }

    [Test]
    public void Discover_PostgreSql_HappyPath_ReturnsSchemaNames()
    {
        var template = PostgreSqlSchemaTemplate();
        var cmd = MockCommandReturning("tenant_a", "tenant_b");

        var result = SchemaDiscovery.Discover(cmd, template);

        Assert.That(result, Is.EqualTo(new List<string> { "tenant_a", "tenant_b" }));
    }

    [Test]
    public void Discover_SkipsNullValues()
    {
        var template = SqlServerSchemaTemplate();
        var cmd = MockCommandReturning("tenant_a", DBNull.Value, "tenant_b");

        var result = SchemaDiscovery.Discover(cmd, template);

        Assert.That(result, Is.EqualTo(new List<string> { "tenant_a", "tenant_b" }));
    }

    [Test]
    public void Discover_EmptyResultSet_ReturnsEmptyList()
    {
        var template = SqlServerSchemaTemplate();
        var cmd = MockCommandReturning();

        var result = SchemaDiscovery.Discover(cmd, template);

        Assert.That(result, Is.Empty);
    }

    [TestCase("dbo")]
    [TestCase("sys")]
    [TestCase("INFORMATION_SCHEMA")]
    [TestCase("guest")]
    [TestCase("db_owner")]
    [TestCase("db_accessadmin")]
    [TestCase("db_securityadmin")]
    [TestCase("db_ddladmin")]
    [TestCase("db_backupoperator")]
    [TestCase("db_datareader")]
    [TestCase("db_datawriter")]
    [TestCase("db_denydatareader")]
    [TestCase("db_denydatawriter")]
    public void Discover_SqlServer_ReservedName_Throws(string reserved)
    {
        var template = SqlServerSchemaTemplate();
        var cmd = MockCommandReturning(reserved);

        var ex = Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
        Assert.That(ex!.Message, Does.Contain(reserved));
        Assert.That(ex.Message.ToLowerInvariant(), Does.Contain("reserved"));
        Assert.That(ex.Message, Does.Contain("regular template"));
    }

    [TestCase("DBO")]
    [TestCase("Dbo")]
    [TestCase("SYS")]
    [TestCase("information_schema")]
    [TestCase("Information_Schema")]
    public void Discover_SqlServer_ReservedName_CaseInsensitive_Throws(string reserved)
    {
        var template = SqlServerSchemaTemplate();
        var cmd = MockCommandReturning(reserved);

        Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
    }

    [TestCase("public")]
    [TestCase("pg_catalog")]
    [TestCase("pg_toast")]
    [TestCase("information_schema")]
    public void Discover_PostgreSql_ReservedName_Throws(string reserved)
    {
        var template = PostgreSqlSchemaTemplate();
        var cmd = MockCommandReturning(reserved);

        var ex = Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
        Assert.That(ex!.Message, Does.Contain(reserved));
        Assert.That(ex.Message, Does.Contain("regular template"));
    }

    [TestCase("PUBLIC")]
    [TestCase("Public")]
    [TestCase("PG_CATALOG")]
    [TestCase("INFORMATION_SCHEMA")]
    public void Discover_PostgreSql_ReservedName_CaseInsensitive_Throws(string reserved)
    {
        var template = PostgreSqlSchemaTemplate();
        var cmd = MockCommandReturning(reserved);

        Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
    }

    [TestCase("pg_temp_5")]
    [TestCase("pg_temp_1")]
    [TestCase("pg_temp_999")]
    [TestCase("PG_TEMP_5")]
    public void Discover_PostgreSql_PgTempWildcard_Throws(string reserved)
    {
        var template = PostgreSqlSchemaTemplate();
        var cmd = MockCommandReturning(reserved);

        Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
    }

    [TestCase("pg_toast_temp_2")]
    [TestCase("pg_toast_temp_1")]
    [TestCase("PG_TOAST_TEMP_5")]
    public void Discover_PostgreSql_PgToastTempWildcard_Throws(string reserved)
    {
        var template = PostgreSqlSchemaTemplate();
        var cmd = MockCommandReturning(reserved);

        Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
    }

    [Test]
    public void Discover_SqlServer_GoodNameAmongReserved_StillThrows()
    {
        // One bad name in the list still fails the whole discovery.
        var template = SqlServerSchemaTemplate();
        var cmd = MockCommandReturning("tenant_a", "dbo", "tenant_b");

        var ex = Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
        Assert.That(ex!.Message, Does.Contain("dbo"));
    }

    [Test]
    public void Discover_MySql_DefensiveThrow()
    {
        // MySQL never legally reaches discovery (validated upstream in Template.Load),
        // but defensive throw catches misuse if it ever does.
        var template = MySqlTemplate();
        var cmd = Substitute.For<IDbCommand>();

        var ex = Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
        Assert.That(ex!.Message, Does.Contain("MySQL"));
    }

    [Test]
    public void Discover_MultiColumnResultSet_Throws()
    {
        // Defensive guard: the design specifies a single-column result set. If the user's
        // SchemaIdentificationScript projects two or more columns, fail loud rather than
        // silently discarding the trailing columns.
        var template = SqlServerSchemaTemplate();
        var cmd = Substitute.For<IDbCommand>();
        var reader = Substitute.For<IDataReader>();
        cmd.ExecuteReader().Returns(reader);
        reader.FieldCount.Returns(2);

        var ex = Assert.Throws<InvalidOperationException>(() => SchemaDiscovery.Discover(cmd, template));
        Assert.That(ex!.Message, Does.Contain(template.Name));
        Assert.That(ex.Message, Does.Contain("2 columns"));
        Assert.That(ex.Message, Does.Contain("single-column"));
    }

    [Test]
    public void Discover_PostgreSql_NameStartingWithPgTempButNotWildcardForm_IsAllowed()
    {
        // The wildcard rule rejects 'pg_temp_<N>' specifically (note trailing underscore).
        // A user-named schema like 'pg_temporary_data' that happens to start with
        // 'pg_temp' (no underscore-N) is NOT a system temp namespace and should be
        // allowed through discovery.
        var template = PostgreSqlSchemaTemplate();
        var cmd = MockCommandReturning("pg_temporary_data");

        Assert.That(SchemaDiscovery.Discover(cmd, template),
            Is.EqualTo(new List<string> { "pg_temporary_data" }));
    }
}

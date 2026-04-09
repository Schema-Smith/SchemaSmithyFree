// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using System;

using NUnit.Framework;
using Schema.IntegrationTests.MySQL;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for SchemaQuench MySQL connection handling.
/// Uses dynamically created test databases via FixtureSetup.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
[Category("MySQL")]
public class SchemaQuenchConnectionTests
{
    private IDbConnection _connection;

    [SetUp]
    public void SetUp()
    {
        // Connect without specifying a database initially
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.ConnectionString);
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void Connection_CanConnect()
    {
        Assert.That(_connection.State, Is.EqualTo(System.Data.ConnectionState.Open));
    }

    [Test]
    public void Connection_CanExecuteSimpleQuery()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT 1";
        var result = command.ExecuteScalar();

        Assert.That(result, Is.Not.Null);
        Assert.That(Convert.ToInt32(result), Is.EqualTo(1));
    }

    [Test]
    public void Connection_CanRetrieveHostname()
    {
        // SchemaQuench uses SELECT @@hostname to identify the server
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT @@hostname";
        var result = command.ExecuteScalar();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ToString(), Is.Not.Empty);
    }

    [Test]
    public void Connection_CanRetrieveVersion()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT @@version";
        var result = command.ExecuteScalar();

        Assert.That(result, Is.Not.Null);
        // MySQL version should start with a number
        Assert.That(result.ToString(), Does.Match(@"^\d+\.\d+"));
    }

    [Test]
    public void Connection_CanQueryInformationSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{FixtureSetup.MainDb}'";
        var result = command.ExecuteScalar();

        Assert.That(result, Is.Not.Null);
        Assert.That(Convert.ToInt32(result), Is.EqualTo(1), "Test database should exist");
    }

    [Test]
    public void Connection_CanSwitchDatabase()
    {
        using var command = _connection.CreateCommand();

        // Switch to test database
        command.CommandText = $"USE `{FixtureSetup.MainDb}`";
        command.ExecuteNonQuery();

        // Verify we're in the test database
        command.CommandText = "SELECT DATABASE()";
        var result = command.ExecuteScalar();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ToString(), Is.EqualTo(FixtureSetup.MainDb));
    }

    [Test]
    public void DbConnectionFactory_CanCreateConnection()
    {
        var factory = DbConnectionFactory.ForPlatform(Platform.MySQL);

        using var connection = factory.GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));
    }

    [Test]
    public void ConnectionStringBuilder_BuildsCorrectFormat()
    {
        var connString = Schema.DataAccess.ConnectionString.Build(Platform.MySQL, "localhost:3306", "testdb", "user", "pass");

        Assert.That(connString, Does.Contain("Server=localhost:3306"));
        Assert.That(connString, Does.Contain("Database=testdb"));
        Assert.That(connString, Does.Contain("Uid=user"));
        Assert.That(connString, Does.Contain("Pwd=pass"));
        Assert.That(connString, Does.Contain("AllowUserVariables=true"));
    }
}

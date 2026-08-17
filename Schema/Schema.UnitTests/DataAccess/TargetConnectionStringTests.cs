// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;

namespace Schema.UnitTests.DataAccess;

/// <summary>
/// Every target connection a run opens — the server-level connection test and each per-database
/// work unit alike — must honor the same three configuration concerns: Target credentials,
/// Target:IntegratedSecurity, and the -Encrypt/-NoEncrypt transport switch. These live at the
/// builder because that is the layer the capability belongs to; testing them through one caller
/// would leave the next caller uncovered, which is exactly how the work-unit path drifted.
/// </summary>
[TestFixture]
public class TargetConnectionStringTests
{
    private IEnvironment _mockEnvironment;

    [SetUp]
    public void SetUp()
    {
        _mockEnvironment = Substitute.For<IEnvironment>();
        _mockEnvironment.CommandLine.Returns("app.exe");
        FactoryContainer.Register<IEnvironment>(_mockEnvironment);
    }

    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    private static IConfigurationRoot Config(params (string Key, string Value)[] entries)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (k, v) in entries) dict[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Test]
    public void Build_SqlServer_IntegratedSecurity_SupersedesConfiguredCredentials()
    {
        // The flagship Target:IntegratedSecurity scenario: a settings file carries a checked-in
        // credential and the operator layers integrated auth over it without editing the file.
        var config = Config(
            ("Target:User", "sa"),
            ("Target:Password", "wrong-on-purpose"),
            ("Target:IntegratedSecurity", "true"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "db", config);

        Assert.That(result, Does.Contain("Integrated Security=True"));
        Assert.That(result, Does.Not.Contain("sa"));
        Assert.That(result, Does.Not.Contain("wrong-on-purpose"));
    }

    [Test]
    public void Build_SqlServer_WithoutIntegratedSecurity_UsesConfiguredCredentials()
    {
        var config = Config(("Target:User", "sa"), ("Target:Password", "pw"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "db", config);

        Assert.That(result, Does.Contain("User ID=sa"));
        Assert.That(result, Does.Contain("Password=pw"));
        Assert.That(result, Does.Not.Contain("Integrated Security"));
    }

    [Test]
    public void Build_SqlServer_NoEncryptSwitch_ReachesConnectionString()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --NoEncrypt");
        var config = Config(("Target:User", "sa"), ("Target:Password", "pw"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "db", config);

        Assert.That(result, Does.Contain("Encrypt=False"));
        Assert.That(result, Does.Not.Contain("Encrypt=True"));
    }

    [Test]
    public void Build_SqlServer_EncryptSwitch_ReachesConnectionString()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --Encrypt");
        var config = Config(("Target:User", "sa"), ("Target:Password", "pw"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "db", config);

        Assert.That(result, Does.Contain("Encrypt=True"));
    }

    [Test]
    public void Build_PostgreSql_NoEncryptSwitch_ReachesConnectionString()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --NoEncrypt");
        var config = Config(("Target:User", "u"), ("Target:Password", "p"));

        var result = TargetConnectionString.Build(Platform.PostgreSQL, "srv", "db", config);

        Assert.That(result, Does.Contain("SSL Mode=Disable"));
    }

    [Test]
    public void Build_MySql_NoEncryptSwitch_ReachesConnectionString()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --NoEncrypt");
        var config = Config(("Target:User", "u"), ("Target:Password", "p"));

        var result = TargetConnectionString.Build(Platform.MySQL, "srv", "db", config);

        Assert.That(result, Does.Contain("SslMode=None"));
    }

    [Test]
    public void Build_PostgreSql_IntegratedSecurityIgnored_StillUsesCredentials()
    {
        // Integrated Security is a SQL Server concept; the flag must not disturb other engines.
        var config = Config(
            ("Target:User", "u"),
            ("Target:Password", "p"),
            ("Target:IntegratedSecurity", "true"));

        var result = TargetConnectionString.Build(Platform.PostgreSQL, "srv", "db", config);

        Assert.That(result, Does.Contain("Username=u"));
        Assert.That(result, Does.Contain("Password=p"));
    }

    [Test]
    public void Build_CarriesTargetConnectionProperties()
    {
        var config = Config(
            ("Target:User", "sa"),
            ("Target:Password", "pw"),
            ("Target:ConnectionProperties:ApplicationName", "SchemaSmith"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "db", config);

        Assert.That(result, Does.Contain("ApplicationName=SchemaSmith"));
    }

    [Test]
    public void Build_TransportSwitch_WinsOverConfiguredConnectionProperty()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --NoEncrypt");
        var config = Config(
            ("Target:User", "sa"),
            ("Target:Password", "pw"),
            ("Target:ConnectionProperties:Encrypt", "True"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "db", config);

        Assert.That(result, Does.Contain("Encrypt=False"));
        Assert.That(result, Does.Not.Contain("Encrypt=True"));
    }

    [Test]
    public void Build_HonorsTargetPort()
    {
        var config = Config(("Target:User", "sa"), ("Target:Password", "pw"), ("Target:Port", "14330"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "db", config);

        Assert.That(result, Does.Contain("srv,14330"));
    }

    [Test]
    public void Build_TargetsTheRequestedDatabase()
    {
        var config = Config(("Target:User", "sa"), ("Target:Password", "pw"));

        var result = TargetConnectionString.Build(Platform.SqlServer, "srv", "PerDbWorkUnit", config);

        Assert.That(result, Does.Contain("Initial Catalog=PerDbWorkUnit"));
    }
}

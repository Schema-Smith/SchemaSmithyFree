// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class ConnectionStringTests
{
    [Test]
    public void Build_SqlServer_WithCredentials_ReturnsCorrectFormat()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "myuser", "mypass");

        Assert.That(result, Does.Contain("data source=myserver"));
        Assert.That(result, Does.Contain("Initial Catalog=mydb"));
        Assert.That(result, Does.Contain("User ID=myuser"));
        Assert.That(result, Does.Contain("Password=mypass"));
    }

    [Test]
    public void Build_SqlServer_WithoutCredentials_UsesIntegratedSecurity()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "", "");

        Assert.That(result, Does.Contain("Integrated Security=True"));
        Assert.That(result, Does.Not.Contain("User ID="));
        Assert.That(result, Does.Not.Contain("Password="));
    }

    [Test]
    public void Build_SqlServer_NullCredentials_UsesIntegratedSecurity()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", null, null);

        Assert.That(result, Does.Contain("Integrated Security=True"));
    }

    [Test]
    public void Build_PostgreSQL_ReturnsCorrectFormat()
    {
        var result = ConnectionString.Build(Platform.PostgreSQL, "pghost", "pgdb", "pguser", "pgpass");

        Assert.That(result, Does.Contain("Host=pghost"));
        Assert.That(result, Does.Contain("Database=pgdb"));
        Assert.That(result, Does.Contain("Username=pguser"));
        Assert.That(result, Does.Contain("Password=pgpass"));
    }

    [Test]
    public void Build_MySQL_ReturnsCorrectFormat()
    {
        var result = ConnectionString.Build(Platform.MySQL, "myhost", "mydb", "myuid", "mypwd");

        Assert.That(result, Does.Contain("Server=myhost"));
        Assert.That(result, Does.Contain("Database=mydb"));
        Assert.That(result, Does.Contain("Uid=myuid"));
        Assert.That(result, Does.Contain("Pwd=mypwd"));
        Assert.That(result, Does.Contain("AllowUserVariables=true"));
    }

    [Test]
    public void Build_SqlServer_WithPort_AppendsCommaPort()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "myuser", "mypass", "1440");

        Assert.That(result, Does.Contain("data source=myserver,1440"));
    }

    [Test]
    public void Build_SqlServer_NullPort_NoCommaAppended()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "myuser", "mypass", null);

        Assert.That(result, Does.Contain("data source=myserver;"));
        Assert.That(result, Does.Not.Contain(","));
    }

    [Test]
    public void Build_PostgreSQL_WithPort_IncludesPortParameter()
    {
        var result = ConnectionString.Build(Platform.PostgreSQL, "pghost", "pgdb", "pguser", "pgpass", "5433");

        Assert.That(result, Does.Contain("Port=5433;"));
        Assert.That(result, Does.Contain("Host=pghost"));
    }

    [Test]
    public void Build_PostgreSQL_NullPort_NoPortParameter()
    {
        var result = ConnectionString.Build(Platform.PostgreSQL, "pghost", "pgdb", "pguser", "pgpass", null);

        Assert.That(result, Does.Not.Contain("Port="));
    }

    [Test]
    public void Build_MySQL_WithPort_IncludesPortParameter()
    {
        var result = ConnectionString.Build(Platform.MySQL, "myhost", "mydb", "myuid", "mypwd", "3307");

        Assert.That(result, Does.Contain("Port=3307;"));
        Assert.That(result, Does.Contain("Server=myhost"));
    }

    [Test]
    public void Build_MySQL_NullPort_NoPortParameter()
    {
        var result = ConnectionString.Build(Platform.MySQL, "myhost", "mydb", "myuid", "mypwd", null);

        Assert.That(result, Does.Not.Contain("Port="));
    }

    [Test]
    public void Build_EmptyPort_TreatedAsNoPort()
    {
        var sqlServer = ConnectionString.Build(Platform.SqlServer, "server", "db", "user", "pass", "");
        var postgres = ConnectionString.Build(Platform.PostgreSQL, "server", "db", "user", "pass", "  ");
        var mysql = ConnectionString.Build(Platform.MySQL, "server", "db", "user", "pass", "");

        Assert.That(sqlServer, Does.Not.Contain(","));
        Assert.That(postgres, Does.Not.Contain("Port="));
        Assert.That(mysql, Does.Not.Contain("Port="));
    }

    [Test]
    public void Build_InvalidPlatform_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            ConnectionString.Build((Platform)999, "server", "db", "user", "pass"));
    }

    [Test]
    public void Build_SqlServer_WithConnectionProperties_AppendsProperties()
    {
        var props = new Dictionary<string, string>
        {
            { "TrustServerCertificate", "True" },
            { "Column Encryption Setting", "Enabled" }
        };
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "myuser", "mypass",
            connectionProperties: props);
        Assert.That(result, Does.Contain("TrustServerCertificate=True"));
        Assert.That(result, Does.Contain("Column Encryption Setting=Enabled"));
    }

    [Test]
    public void Build_PostgreSQL_WithConnectionProperties_AppendsProperties()
    {
        var props = new Dictionary<string, string>
        {
            { "Trust Server Certificate", "True" },
            { "SSL Mode", "Require" }
        };
        var result = ConnectionString.Build(Platform.PostgreSQL, "pghost", "pgdb", "pguser", "pgpass",
            connectionProperties: props);
        Assert.That(result, Does.Contain("Trust Server Certificate=True"));
        Assert.That(result, Does.Contain("SSL Mode=Require"));
    }

    [Test]
    public void Build_MySQL_WithConnectionProperties_AppendsProperties()
    {
        var props = new Dictionary<string, string> { { "SslMode", "Required" } };
        var result = ConnectionString.Build(Platform.MySQL, "myhost", "mydb", "myuid", "mypwd",
            connectionProperties: props);
        Assert.That(result, Does.Contain("SslMode=Required"));
    }

    [Test]
    public void Build_NullConnectionProperties_ProducesValidConnectionString()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "myuser", "mypass",
            connectionProperties: null);
        Assert.That(result, Does.Contain("data source=myserver"));
        Assert.That(result, Does.Contain("Initial Catalog=mydb"));
    }

    [Test]
    public void Build_EmptyConnectionProperties_ProducesValidConnectionString()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "myuser", "mypass",
            connectionProperties: new Dictionary<string, string>());
        Assert.That(result, Does.Contain("data source=myserver"));
    }

    [Test]
    public void Build_MySQL_StructuralPropertySkipped_WhenUserTriesToOverride()
    {
        var props = new Dictionary<string, string> { { "AllowUserVariables", "false" } };
        var result = ConnectionString.Build(Platform.MySQL, "myhost", "mydb", "myuid", "mypwd",
            connectionProperties: props);
        Assert.That(result, Does.Contain("AllowUserVariables=true"));
        var count = result.Split("AllowUserVariables").Length - 1;
        Assert.That(count, Is.EqualTo(1), "AllowUserVariables should appear exactly once");
    }

    [Test]
    public void Build_SqlServer_NoHardcodedTrustServerCertificate()
    {
        var result = ConnectionString.Build(Platform.SqlServer, "myserver", "mydb", "myuser", "mypass");
        Assert.That(result, Does.Not.Contain("TrustServerCertificate"));
        Assert.That(result, Does.Not.Contain("ApplicationIntent"));
    }

    [Test]
    public void Build_PostgreSQL_NoHardcodedTrustServerCertificate()
    {
        var result = ConnectionString.Build(Platform.PostgreSQL, "pghost", "pgdb", "pguser", "pgpass");
        Assert.That(result, Does.Not.Contain("Trust Server Certificate"));
    }
}

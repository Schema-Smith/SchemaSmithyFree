// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Schema.DataAccess;

namespace Schema.UnitTests;

[TestFixture]
public class ConnectionStringTests
{
    [Test]
    public void Build_WithCredentials_ReturnsCorrectFormat()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass");

        Assert.That(result, Does.Contain("data source=myserver"));
        Assert.That(result, Does.Contain("Initial Catalog=mydb"));
        Assert.That(result, Does.Contain("User ID=myuser"));
        Assert.That(result, Does.Contain("Password=mypass"));
    }

    [Test]
    public void Build_WithoutCredentials_UsesIntegratedSecurity()
    {
        var result = ConnectionString.Build("myserver", "mydb", "", "");

        Assert.That(result, Does.Contain("Integrated Security=True"));
        Assert.That(result, Does.Not.Contain("User ID="));
        Assert.That(result, Does.Not.Contain("Password="));
    }

    [Test]
    public void Build_NullCredentials_UsesIntegratedSecurity()
    {
        var result = ConnectionString.Build("myserver", "mydb", null, null);

        Assert.That(result, Does.Contain("Integrated Security=True"));
    }

    [Test]
    public void Build_WithPort_AppendsCommaPort()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass", "1450");

        Assert.That(result, Does.Contain("data source=myserver,1450"));
    }

    [Test]
    public void Build_NullPort_NoCommaAppended()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass", null);

        Assert.That(result, Does.Contain("data source=myserver;"));
        Assert.That(result, Does.Not.Contain("myserver,"));
    }

    [Test]
    public void Build_EmptyPort_TreatedAsNoPort()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass", "");

        Assert.That(result, Does.Not.Contain("myserver,"));
    }

    [Test]
    public void Build_WhitespacePort_TreatedAsNoPort()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass", "  ");

        Assert.That(result, Does.Not.Contain("myserver,"));
    }

    [Test]
    public void Build_WithConnectionProperties_AppendsProperties()
    {
        var props = new Dictionary<string, string>
        {
            { "TrustServerCertificate", "True" },
            { "Column Encryption Setting", "Enabled" }
        };

        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass",
            connectionProperties: props);

        Assert.That(result, Does.Contain("TrustServerCertificate=True"));
        Assert.That(result, Does.Contain("Column Encryption Setting=Enabled"));
    }

    [Test]
    public void Build_NullConnectionProperties_ProducesValidConnectionString()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass",
            connectionProperties: null);

        Assert.That(result, Does.Contain("data source=myserver"));
        Assert.That(result, Does.Contain("Initial Catalog=mydb"));
    }

    [Test]
    public void Build_EmptyConnectionProperties_ProducesValidConnectionString()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass",
            connectionProperties: new Dictionary<string, string>());

        Assert.That(result, Does.Contain("data source=myserver"));
    }

    [Test]
    public void Build_NoHardcodedTrustServerCertificate()
    {
        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass");

        Assert.That(result, Does.Not.Contain("TrustServerCertificate"));
        Assert.That(result, Does.Not.Contain("ApplicationIntent"));
    }

    [Test]
    public void Build_WithPortAndConnectionProperties_BothApplied()
    {
        var props = new Dictionary<string, string> { { "TrustServerCertificate", "True" } };

        var result = ConnectionString.Build("myserver", "mydb", "myuser", "mypass", "1433", props);

        Assert.That(result, Does.Contain("data source=myserver,1433"));
        Assert.That(result, Does.Contain("TrustServerCertificate=True"));
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Isolators;
using SchemaSmith.Pro;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class SqlServerConnectionFactoryTests
{
    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    [Test]
    public void GetDbConnection_ReturnsOpenableConnection()
    {
        var factory = new SqlServerConnectionFactory();
        var connection = factory.GetDbConnection("Data Source=localhost;Initial Catalog=test;Integrated Security=True;TrustServerCertificate=True;");

        Assert.That(connection, Is.Not.Null);
        Assert.That(connection, Is.InstanceOf<IDbConnection>());
        connection.Dispose();
    }

    [Test]
    public void GetFromFactory_ReturnsInstance()
    {
        var factory = SqlServerConnectionFactory.GetFromFactory();

        Assert.That(factory, Is.Not.Null);
        Assert.That(factory, Is.InstanceOf<IDbConnectionFactory>());
        Assert.That(factory, Is.InstanceOf<SqlServerConnectionFactory>());
    }

    [Test]
    public void GetFromFactory_WithRegisteredMock_ReturnsMock()
    {
        var mock = NSubstitute.Substitute.For<IDbConnectionFactory>();
        FactoryContainer.Register(mock);

        var factory = SqlServerConnectionFactory.GetFromFactory();

        Assert.That(factory, Is.SameAs(mock));
    }
}

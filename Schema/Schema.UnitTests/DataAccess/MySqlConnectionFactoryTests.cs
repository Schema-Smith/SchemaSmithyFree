// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Isolators;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class MySqlConnectionFactoryTests
{
    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    [Test]
    public void GetDbConnection_ReturnsConnection()
    {
        var factory = new MySqlConnectionFactory();
        var connection = factory.GetDbConnection("Server=localhost;Database=test;Uid=user;Pwd=pass;");

        Assert.That(connection, Is.Not.Null);
        Assert.That(connection, Is.InstanceOf<IDbConnection>());
        connection.Dispose();
    }

    [Test]
    public void GetFromFactory_ReturnsInstance()
    {
        var factory = MySqlConnectionFactory.GetFromFactory();

        Assert.That(factory, Is.Not.Null);
        Assert.That(factory, Is.InstanceOf<IDbConnectionFactory>());
        Assert.That(factory, Is.InstanceOf<MySqlConnectionFactory>());
    }

    [Test]
    public void GetFromFactory_WithRegisteredMock_ReturnsMock()
    {
        var mock = NSubstitute.Substitute.For<IDbConnectionFactory>();
        FactoryContainer.Register(mock);

        var factory = MySqlConnectionFactory.GetFromFactory();

        Assert.That(factory, Is.SameAs(mock));
    }
}

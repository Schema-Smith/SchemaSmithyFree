// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Isolators;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class PostgreSqlConnectionFactoryTests
{
    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    [Test]
    public void GetDbConnection_ReturnsConnection()
    {
        var factory = new PostgreSqlConnectionFactory();
        var connection = factory.GetDbConnection("Host=localhost;Database=test;Username=user;Password=pass;");

        Assert.That(connection, Is.Not.Null);
        Assert.That(connection, Is.InstanceOf<IDbConnection>());
        connection.Dispose();
    }

    [Test]
    public void GetDbConnection_CachesOneDataSourcePerConnectionString()
    {
        var factory = new PostgreSqlConnectionFactory();
        var cs = $"Host=localhost;Database=cache_{System.Guid.NewGuid():N};Username=u;Password=p;";
        var before = PostgreSqlConnectionFactory.CachedDataSourceCount;

        using (factory.GetDbConnection(cs)) { }
        var afterFirst = PostgreSqlConnectionFactory.CachedDataSourceCount;
        using (factory.GetDbConnection(cs)) { } // same connection string -> reuse the cached data source
        var afterSecond = PostgreSqlConnectionFactory.CachedDataSourceCount;

        Assert.That(afterFirst, Is.EqualTo(before + 1), "a new connection string creates exactly one cached data source");
        Assert.That(afterSecond, Is.EqualTo(afterFirst), "the same connection string reuses the cached data source (no per-call leak)");
    }

    [Test]
    public void GetFromFactory_ReturnsInstance()
    {
        var factory = PostgreSqlConnectionFactory.GetFromFactory();

        Assert.That(factory, Is.Not.Null);
        Assert.That(factory, Is.InstanceOf<IDbConnectionFactory>());
        Assert.That(factory, Is.InstanceOf<PostgreSqlConnectionFactory>());
    }

    [Test]
    public void GetFromFactory_WithRegisteredMock_ReturnsMock()
    {
        var mock = NSubstitute.Substitute.For<IDbConnectionFactory>();
        FactoryContainer.Register(mock);

        var factory = PostgreSqlConnectionFactory.GetFromFactory();

        Assert.That(factory, Is.SameAs(mock));
    }
}

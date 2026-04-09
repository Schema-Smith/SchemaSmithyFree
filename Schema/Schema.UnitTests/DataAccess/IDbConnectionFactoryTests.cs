// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class DbConnectionFactoryDispatcherTests
{
    [Test]
    public void ForPlatform_SqlServer_ReturnsSqlServerConnectionFactory()
    {
        var factory = DbConnectionFactory.ForPlatform(Platform.SqlServer);

        Assert.That(factory, Is.InstanceOf<SqlServerConnectionFactory>());
    }

    [Test]
    public void ForPlatform_PostgreSQL_ReturnsPostgreSqlConnectionFactory()
    {
        var factory = DbConnectionFactory.ForPlatform(Platform.PostgreSQL);

        Assert.That(factory, Is.InstanceOf<PostgreSqlConnectionFactory>());
    }

    [Test]
    public void ForPlatform_MySQL_ReturnsMySqlConnectionFactory()
    {
        var factory = DbConnectionFactory.ForPlatform(Platform.MySQL);

        Assert.That(factory, Is.InstanceOf<MySqlConnectionFactory>());
    }

    [Test]
    public void ForPlatform_InvalidPlatform_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DbConnectionFactory.ForPlatform((Platform)999));
    }

    [Test]
    public void ForPlatform_AllValidPlatforms_ReturnIDbConnectionFactory()
    {
        foreach (Platform platform in Enum.GetValues(typeof(Platform)))
        {
            if (platform == Platform.Unknown) continue;
            var factory = DbConnectionFactory.ForPlatform(platform);
            Assert.That(factory, Is.Not.Null);
            Assert.That(factory, Is.InstanceOf<IDbConnectionFactory>());
        }
    }
}

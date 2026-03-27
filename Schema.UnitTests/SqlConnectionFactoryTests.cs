// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System.Data;
using Schema.DataAccess;

namespace Schema.UnitTests;

public class SqlConnectionFactoryTests
{
    [Test]
    public void ShouldCreateNewConnection()
    {
        var connection = SqlConnectionFactory.GetFromFactory().GetSqlConnection("Server=.;Database=master;Trusted_Connection=True;");
        Assert.Multiple(() =>
        {
            Assert.That(connection, Is.Not.Null);
            Assert.That(connection, Is.AssignableTo<IDbConnection>());
            Assert.That(connection.Database, Is.EqualTo("master"));
        });
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class SqlServerForeignKeyTests
    {
        [Test]
        public void InheritsFromForeignKey()
        {
            Assert.That(new SqlServerForeignKey(), Is.InstanceOf<ForeignKey>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var fk = new SqlServerForeignKey();
            Assert.That(fk.RelatedTableSchema, Is.EqualTo("dbo"));
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var fk = new SqlServerForeignKey
            {
                Name = "FK_Order_Customer",
                Columns = "CustomerId",
                RelatedTableSchema = "sales",
                RelatedTable = "Customer",
                RelatedColumns = "Id",
                DeleteAction = "CASCADE"
            };

            var json = JsonConvert.SerializeObject(fk);
            var deserialized = JsonConvert.DeserializeObject<SqlServerForeignKey>(json);

            Assert.That(deserialized.Name, Is.EqualTo("FK_Order_Customer"));
            Assert.That(deserialized.RelatedTableSchema, Is.EqualTo("sales"));
            Assert.That(deserialized.RelatedTable, Is.EqualTo("Customer"));
            Assert.That(deserialized.DeleteAction, Is.EqualTo("CASCADE"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var fk = new SqlServerForeignKey { Name = "FK_Test", Columns = "Col1", RelatedTable = "T2", RelatedColumns = "Id" };
            var json = JsonConvert.SerializeObject(fk);

            Assert.That(json, Does.Not.Contain("Deferrable"));
            Assert.That(json, Does.Not.Contain("InitiallyDeferred"));
            Assert.That(json, Does.Not.Contain("MatchType"));
        }
    }
}

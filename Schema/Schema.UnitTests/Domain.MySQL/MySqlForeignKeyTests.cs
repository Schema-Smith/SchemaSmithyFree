// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.MySQL;

namespace Schema.UnitTests.Domain.MySQL
{
    [TestFixture]
    public class MySqlForeignKeyTests
    {
        [Test]
        public void InheritsFromForeignKey()
        {
            Assert.That(new MySqlForeignKey(), Is.InstanceOf<ForeignKey>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var fk = new MySqlForeignKey();
            Assert.That(fk.RelatedTableSchema, Is.EqualTo(""));
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var fk = new MySqlForeignKey
            {
                Name = "fk_order_customer",
                Columns = "customer_id",
                RelatedTableSchema = "shop_db",
                RelatedTable = "customer",
                RelatedColumns = "id",
                DeleteAction = "CASCADE"
            };

            var json = JsonConvert.SerializeObject(fk);
            var deserialized = JsonConvert.DeserializeObject<MySqlForeignKey>(json);

            Assert.That(deserialized.RelatedTableSchema, Is.EqualTo("shop_db"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var fk = new MySqlForeignKey { Name = "fk_test", Columns = "col1", RelatedTable = "t2", RelatedColumns = "id" };
            var json = JsonConvert.SerializeObject(fk);

            Assert.That(json, Does.Not.Contain("Deferrable"));
            Assert.That(json, Does.Not.Contain("InitiallyDeferred"));
            Assert.That(json, Does.Not.Contain("MatchType"));
        }
    }
}

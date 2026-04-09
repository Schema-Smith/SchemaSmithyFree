// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class ForeignKeyTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var fk = new ForeignKey();

            Assert.That(fk.Name, Is.EqualTo(""));
            Assert.That(fk.Columns, Is.EqualTo(""));
            Assert.That(fk.RelatedTable, Is.EqualTo(""));
            Assert.That(fk.RelatedColumns, Is.EqualTo(""));
            Assert.That(fk.DeleteAction, Is.Null);
            Assert.That(fk.UpdateAction, Is.Null);
            Assert.That(fk.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var fk = new ForeignKey
            {
                Name = "FK_Order_Customer",
                Columns = "CustomerId",
                RelatedTable = "Customer",
                RelatedColumns = "Id",
                DeleteAction = "CASCADE",
                UpdateAction = "NO ACTION",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(fk);
            var deserialized = JsonConvert.DeserializeObject<ForeignKey>(json);

            Assert.That(deserialized.Name, Is.EqualTo("FK_Order_Customer"));
            Assert.That(deserialized.Columns, Is.EqualTo("CustomerId"));
            Assert.That(deserialized.RelatedTable, Is.EqualTo("Customer"));
            Assert.That(deserialized.RelatedColumns, Is.EqualTo("Id"));
            Assert.That(deserialized.DeleteAction, Is.EqualTo("CASCADE"));
            Assert.That(deserialized.UpdateAction, Is.EqualTo("NO ACTION"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

    }
}

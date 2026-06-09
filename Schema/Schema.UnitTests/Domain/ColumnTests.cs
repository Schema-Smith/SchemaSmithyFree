// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class ColumnTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var column = new Column();

            Assert.That(column.Name, Is.EqualTo(""));
            Assert.That(column.DataType, Is.EqualTo(""));
            Assert.That(column.Nullable, Is.False);
            Assert.That(column.Default, Is.Null);
            Assert.That(column.ShouldApplyExpression, Is.Null);
            Assert.That(column.OldName, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var column = new Column
            {
                Name = "CustomerId",
                DataType = "int",
                Nullable = true,
                Default = "0",
                ShouldApplyExpression = "SELECT 1",
                OldName = "CustId"
            };

            var json = JsonConvert.SerializeObject(column);
            var deserialized = JsonConvert.DeserializeObject<Column>(json);

            Assert.That(deserialized.Name, Is.EqualTo("CustomerId"));
            Assert.That(deserialized.DataType, Is.EqualTo("int"));
            Assert.That(deserialized.Nullable, Is.True);
            Assert.That(deserialized.Default, Is.EqualTo("0"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
            Assert.That(deserialized.OldName, Is.EqualTo("CustId"));
        }

        [Test]
        public void JsonSerialization_OmitsNullProperties()
        {
            var column = new Column { Name = "Id", DataType = "int" };

            var json = JsonConvert.SerializeObject(column, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            Assert.That(json, Does.Not.Contain("Default"));
            Assert.That(json, Does.Not.Contain("ShouldApplyExpression"));
            Assert.That(json, Does.Not.Contain("OldName"));
        }

        [Test]
        public void JsonRoundTrip_PreservesVariantName()
        {
            var column = new Column { Name = "RegionCode", DataType = "char(2)", ShouldApplyExpression = "1=1", VariantName = "EU region" };
            var json = JsonConvert.SerializeObject(column);
            var deserialized = JsonConvert.DeserializeObject<Column>(json);
            Assert.That(deserialized.VariantName, Is.EqualTo("EU region"));
        }

    }
}

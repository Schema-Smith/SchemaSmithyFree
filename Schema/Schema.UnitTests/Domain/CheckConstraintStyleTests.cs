// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class CheckConstraintStyleTests
    {
        private class TestObject
        {
            public CheckConstraintStyle ConstraintStyle { get; set; }
        }

        [Test]
        public void ColumnLevel_IsDefaultValue()
        {
            Assert.That(default(CheckConstraintStyle), Is.EqualTo(CheckConstraintStyle.ColumnLevel));
        }

        [TestCase(CheckConstraintStyle.ColumnLevel, "ColumnLevel")]
        [TestCase(CheckConstraintStyle.TableLevel, "TableLevel")]
        public void AllValues_RoundTripThroughJsonSerialization(CheckConstraintStyle value, string expectedString)
        {
            var json = JsonConvert.SerializeObject(value);
            Assert.That(json, Is.EqualTo($"\"{expectedString}\""));

            var deserialized = JsonConvert.DeserializeObject<CheckConstraintStyle>(json);
            Assert.That(deserialized, Is.EqualTo(value));
        }

        [Test]
        public void Serialize_WithDefaultValueIgnore_OmitsColumnLevelProperty()
        {
            var obj = new TestObject { ConstraintStyle = CheckConstraintStyle.ColumnLevel };
            var json = JsonHelper.Serialize(obj);
            Assert.That(json, Does.Not.Contain("ConstraintStyle"));
        }

        [Test]
        public void Serialize_WithDefaultValueIgnore_IncludesTableLevelProperty()
        {
            var obj = new TestObject { ConstraintStyle = CheckConstraintStyle.TableLevel };
            var json = JsonHelper.Serialize(obj);
            Assert.That(json, Does.Contain("ConstraintStyle"));
            Assert.That(json, Does.Contain("TableLevel"));
        }
    }
}

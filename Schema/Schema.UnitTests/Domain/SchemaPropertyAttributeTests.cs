// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class SchemaPropertyAttributeTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var attr = new SchemaPropertyAttribute();

            Assert.That(attr.Pattern, Is.Null);
            Assert.That(double.IsNaN(attr.Minimum), Is.True);
            Assert.That(double.IsNaN(attr.Maximum), Is.True);
            Assert.That(double.IsNaN(attr.MultipleOf), Is.True);
            Assert.That(attr.Format, Is.Null);
            Assert.That(attr.Description, Is.Null);
            Assert.That(attr.Deprecated, Is.False);
            Assert.That(attr.Required, Is.False);
            Assert.That(attr.MaxLength, Is.EqualTo(-1));
        }

        [Test]
        public void CanSetAllProperties()
        {
            var attr = new SchemaPropertyAttribute
            {
                Pattern = "^[a-z]+$",
                Minimum = 0,
                Maximum = 100,
                MultipleOf = 5,
                Format = "date-time",
                Description = "A test property",
                Deprecated = true,
                Required = true,
                MaxLength = 128
            };

            Assert.That(attr.Pattern, Is.EqualTo("^[a-z]+$"));
            Assert.That(attr.Minimum, Is.EqualTo(0));
            Assert.That(attr.Maximum, Is.EqualTo(100));
            Assert.That(attr.MultipleOf, Is.EqualTo(5));
            Assert.That(attr.Format, Is.EqualTo("date-time"));
            Assert.That(attr.Description, Is.EqualTo("A test property"));
            Assert.That(attr.Deprecated, Is.True);
            Assert.That(attr.Required, Is.True);
            Assert.That(attr.MaxLength, Is.EqualTo(128));
        }

        [Test]
        public void AttributeUsage_IsPropertyOnly()
        {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
                typeof(SchemaPropertyAttribute), typeof(AttributeUsageAttribute));

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Property));
        }

        [Test]
        public void Required_DefaultsToFalse()
        {
            var attr = new SchemaPropertyAttribute();
            Assert.That(attr.Required, Is.False);
        }

        [Test]
        public void Required_CanBeSetToTrue()
        {
            var attr = new SchemaPropertyAttribute { Required = true };
            Assert.That(attr.Required, Is.True);
        }
    }
}

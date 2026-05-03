// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class XmlIndexTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromDynamicBase()
        {
            Assert.That(new XmlIndex(), Is.InstanceOf<DynamicBase>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var xi = new XmlIndex();

            Assert.That(xi.Name, Is.Null);
            Assert.That(xi.IsPrimary, Is.False);
            Assert.That(xi.Column, Is.Null);
            Assert.That(xi.PrimaryIndex, Is.Null);
            Assert.That(xi.SecondaryIndexType, Is.Null);
            Assert.That(xi.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var xi = new XmlIndex
            {
                Name = "IX_XML_Config",
                IsPrimary = true,
                Column = "ConfigXml",
                PrimaryIndex = null,
                SecondaryIndexType = null,
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(xi);
            var deserialized = JsonConvert.DeserializeObject<XmlIndex>(json);

            Assert.That(deserialized.Name, Is.EqualTo("IX_XML_Config"));
            Assert.That(deserialized.IsPrimary, Is.True);
            Assert.That(deserialized.Column, Is.EqualTo("ConfigXml"));
        }

        [Test]
        public void SecondaryXmlIndex_RoundTrip()
        {
            var xi = new XmlIndex
            {
                Name = "IX_XML_Config_Value",
                IsPrimary = false,
                Column = "ConfigXml",
                PrimaryIndex = "IX_XML_Config",
                SecondaryIndexType = "VALUE"
            };

            var json = JsonConvert.SerializeObject(xi);
            var deserialized = JsonConvert.DeserializeObject<XmlIndex>(json);

            Assert.That(deserialized.PrimaryIndex, Is.EqualTo("IX_XML_Config"));
            Assert.That(deserialized.SecondaryIndexType, Is.EqualTo("VALUE"));
        }
    }
}

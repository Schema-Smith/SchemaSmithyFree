// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class FullTextIndexTests
    {
        private static readonly JsonSerializerSettings NullIgnore = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [Test]
        public void InheritsFromDynamicBase()
        {
            Assert.That(new FullTextIndex(), Is.InstanceOf<DynamicBase>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var fti = new FullTextIndex();

            Assert.That(fti.FullTextCatalog, Is.Null);
            Assert.That(fti.KeyIndex, Is.Null);
            Assert.That(fti.ChangeTracking, Is.EqualTo("AUTO"));
            Assert.That(fti.StopList, Is.EqualTo("SYSTEM"));
            Assert.That(fti.Columns, Is.Null);
            Assert.That(fti.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var fti = new FullTextIndex
            {
                FullTextCatalog = "FTC_Main",
                KeyIndex = "PK_Docs",
                ChangeTracking = "MANUAL",
                StopList = "MyStopList",
                Columns = "Title, Body",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(fti);
            var deserialized = JsonConvert.DeserializeObject<FullTextIndex>(json);

            Assert.That(deserialized.FullTextCatalog, Is.EqualTo("FTC_Main"));
            Assert.That(deserialized.KeyIndex, Is.EqualTo("PK_Docs"));
            Assert.That(deserialized.ChangeTracking, Is.EqualTo("MANUAL"));
            Assert.That(deserialized.StopList, Is.EqualTo("MyStopList"));
            Assert.That(deserialized.Columns, Is.EqualTo("Title, Body"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

        [Test]
        public void JsonSerialization_OmitsNullProperties()
        {
            var fti = new FullTextIndex { FullTextCatalog = "FTC", KeyIndex = "PK", Columns = "Col1" };
            var json = JsonConvert.SerializeObject(fti, NullIgnore);

            Assert.That(json, Does.Not.Contain("ShouldApplyExpression"));
        }
    }
}

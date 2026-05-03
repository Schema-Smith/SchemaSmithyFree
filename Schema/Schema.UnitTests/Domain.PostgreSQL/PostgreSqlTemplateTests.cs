// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.PostgreSQL;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlTemplateTests
    {
        [Test]
        public void InheritsFromTemplate()
        {
            Assert.That(new PostgreSqlTemplate(), Is.InstanceOf<Template>());
        }

        [Test]
        public void JsonRoundTrip_PreservesBaseProperties()
        {
            var template = new PostgreSqlTemplate
            {
                Name = "Main",
                DatabaseIdentificationScript = "SELECT current_database()"
            };

            var json = JsonConvert.SerializeObject(template);
            var deserialized = JsonConvert.DeserializeObject<PostgreSqlTemplate>(json);

            Assert.That(deserialized.Name, Is.EqualTo("Main"));
            Assert.That(deserialized.DatabaseIdentificationScript, Is.EqualTo("SELECT current_database()"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var template = new PostgreSqlTemplate { Name = "Test" };
            var json = JsonConvert.SerializeObject(template);

            Assert.That(json, Does.Not.Contain("ServerToQuench"));
        }
    }
}

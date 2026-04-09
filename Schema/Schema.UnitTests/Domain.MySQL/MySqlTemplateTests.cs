// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.MySQL;

namespace Schema.UnitTests.Domain.MySQL
{
    [TestFixture]
    public class MySqlTemplateTests
    {
        [Test]
        public void InheritsFromTemplate()
        {
            Assert.That(new MySqlTemplate(), Is.InstanceOf<Template>());
        }

        [Test]
        public void JsonRoundTrip_PreservesBaseProperties()
        {
            var template = new MySqlTemplate
            {
                Name = "Main",
                DatabaseIdentificationScript = "SELECT DATABASE()"
            };

            var json = JsonConvert.SerializeObject(template);
            var deserialized = JsonConvert.DeserializeObject<MySqlTemplate>(json);

            Assert.That(deserialized.Name, Is.EqualTo("Main"));
            Assert.That(deserialized.DatabaseIdentificationScript, Is.EqualTo("SELECT DATABASE()"));
        }

        [Test]
        public void NoPhantomProperties_FromOtherPlatforms()
        {
            var template = new MySqlTemplate { Name = "Test" };
            var json = JsonConvert.SerializeObject(template);

            Assert.That(json, Does.Not.Contain("ServerToQuench"));
        }
    }
}

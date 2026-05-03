// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class SqlServerTemplateTests
    {
        [Test]
        public void InheritsFromTemplate()
        {
            Assert.That(new SqlServerTemplate(), Is.InstanceOf<Template>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var template = new SqlServerTemplate();
            Assert.That(template.ServerToQuench, Is.EqualTo(ServerToQuench.Primary));
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var template = new SqlServerTemplate
            {
                Name = "Main",
                DatabaseIdentificationScript = "SELECT DB_NAME()",
                ServerToQuench = ServerToQuench.Both
            };

            var json = JsonConvert.SerializeObject(template);
            var deserialized = JsonConvert.DeserializeObject<SqlServerTemplate>(json);

            Assert.That(deserialized.Name, Is.EqualTo("Main"));
            Assert.That(deserialized.ServerToQuench, Is.EqualTo(ServerToQuench.Both));
        }

        [Test]
        public void ServerToQuench_SerializesAsString()
        {
            var template = new SqlServerTemplate
            {
                Name = "Test",
                ServerToQuench = ServerToQuench.Secondary
            };

            var json = JsonConvert.SerializeObject(template);
            Assert.That(json, Does.Contain("\"Secondary\""));
        }

        [TestCase(ServerToQuench.Primary, true, false)]
        [TestCase(ServerToQuench.Secondary, false, true)]
        [TestCase(ServerToQuench.Both, true, true)]
        public void QuenchOnPrimary_QuenchOnSecondary_CorrectForServerToQuench(
            ServerToQuench stq, bool expectedPrimary, bool expectedSecondary)
        {
            var template = new SqlServerTemplate { ServerToQuench = stq };
            Assert.That(template.QuenchOnPrimary, Is.EqualTo(expectedPrimary));
            Assert.That(template.QuenchOnSecondary, Is.EqualTo(expectedSecondary));
        }

        [Test]
        public void QuenchOnPrimary_QuenchOnSecondary_NotSerialized()
        {
            var template = new SqlServerTemplate
            {
                Name = "Test",
                ServerToQuench = ServerToQuench.Both
            };

            var json = JsonConvert.SerializeObject(template);
            Assert.That(json, Does.Not.Contain("QuenchOnPrimary"));
            Assert.That(json, Does.Not.Contain("QuenchOnSecondary"));
        }
    }
}

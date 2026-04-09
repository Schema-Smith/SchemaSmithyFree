// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Reflection;
using Newtonsoft.Json;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class ScriptFolderTests
    {
        // ScriptFolder is abstract in concept but concrete in declaration; test via direct instantiation
        // and via subclasses TemplateFolder and ProductFolder.

        [Test]
        public void ScriptFolder_ObjectType_DefaultIsNone()
        {
            var folder = new TemplateFolder();
            Assert.That(folder.ObjectType, Is.EqualTo(ScriptObjectType.None));
        }

        [Test]
        public void ScriptFolder_Serialize_WithObjectTypeNone_OmitsObjectType()
        {
            var folder = new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.Objects };

            var json = JsonHelper.Serialize(folder);

            Assert.That(json, Does.Not.Contain("ObjectType"));
        }

        [Test]
        public void ScriptFolder_Serialize_WithObjectTypeViews_IncludesObjectType()
        {
            var folder = new TemplateFolder
            {
                FolderPath = "Views",
                QuenchSlot = TemplateQuenchSlot.Objects,
                ObjectType = ScriptObjectType.Views
            };

            var json = JsonHelper.Serialize(folder);

            Assert.That(json, Does.Contain("\"ObjectType\""));
            Assert.That(json, Does.Contain("\"Views\""));
        }

        [Test]
        public void ScriptFolder_ObjectType_HasSchemaPropertyAttribute()
        {
            var prop = typeof(ScriptFolder).GetProperty(nameof(ScriptFolder.ObjectType));
            var attr = prop?.GetCustomAttribute<SchemaPropertyAttribute>();

            Assert.That(attr, Is.Not.Null);
        }

        [Test]
        public void TemplateFolder_Clone_PreservesObjectType()
        {
            var original = new TemplateFolder
            {
                FolderPath = "Procedures",
                QuenchSlot = TemplateQuenchSlot.Objects,
                ObjectType = ScriptObjectType.Procedures
            };

            var clone = original.Clone();

            Assert.That(clone.ObjectType, Is.EqualTo(ScriptObjectType.Procedures));
            Assert.That(clone.FolderPath, Is.EqualTo("Procedures"));
            Assert.That(clone.QuenchSlot, Is.EqualTo(TemplateQuenchSlot.Objects));
        }

        [Test]
        public void TemplateFolder_Clone_WithObjectTypeNone_PreservesNone()
        {
            var original = new TemplateFolder
            {
                FolderPath = "Before Scripts",
                QuenchSlot = TemplateQuenchSlot.Before,
                ObjectType = ScriptObjectType.None
            };

            var clone = original.Clone();

            Assert.That(clone.ObjectType, Is.EqualTo(ScriptObjectType.None));
        }

        [Test]
        public void ProductFolder_Serialize_WithObjectTypeViews_IncludesObjectType()
        {
            var folder = new ProductFolder
            {
                FolderPath = "Views",
                QuenchSlot = ProductQuenchSlot.Before,
                ObjectType = ScriptObjectType.Views
            };

            var json = JsonHelper.Serialize(folder);

            Assert.That(json, Does.Contain("\"ObjectType\""));
            Assert.That(json, Does.Contain("\"Views\""));
        }

        [Test]
        public void ProductFolder_Serialize_WithObjectTypeNone_OmitsObjectType()
        {
            var folder = new ProductFolder
            {
                FolderPath = "Scripts",
                QuenchSlot = ProductQuenchSlot.Before
            };

            var json = JsonHelper.Serialize(folder);

            Assert.That(json, Does.Not.Contain("ObjectType"));
        }

        [Test]
        public void TemplateFolder_JsonRoundTrip_PreservesObjectType()
        {
            var folder = new TemplateFolder
            {
                FolderPath = "Functions",
                QuenchSlot = TemplateQuenchSlot.Objects,
                ObjectType = ScriptObjectType.Functions
            };

            var json = JsonConvert.SerializeObject(folder);
            var deserialized = JsonConvert.DeserializeObject<TemplateFolder>(json);

            Assert.That(deserialized.ObjectType, Is.EqualTo(ScriptObjectType.Functions));
        }

        [Test]
        public void ProductFolder_JsonRoundTrip_PreservesObjectType()
        {
            var folder = new ProductFolder
            {
                FolderPath = "Procedures",
                QuenchSlot = ProductQuenchSlot.Before,
                ObjectType = ScriptObjectType.Procedures
            };

            var json = JsonConvert.SerializeObject(folder);
            var deserialized = JsonConvert.DeserializeObject<ProductFolder>(json);

            Assert.That(deserialized.ObjectType, Is.EqualTo(ScriptObjectType.Procedures));
        }
    }
}

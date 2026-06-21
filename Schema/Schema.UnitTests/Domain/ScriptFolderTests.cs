// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
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

        // ---- ShouldApplyExpression (folder-level conditional deployment, #260) ----

        [Test]
        public void ScriptFolder_ShouldApplyExpression_DefaultIsNull()
        {
            Assert.That(new TemplateFolder().ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void ScriptFolder_ShouldApplyExpression_HasSchemaPropertyAttribute()
        {
            var prop = typeof(ScriptFolder).GetProperty(nameof(ScriptFolder.ShouldApplyExpression));
            var attr = prop?.GetCustomAttribute<SchemaPropertyAttribute>();

            Assert.That(attr, Is.Not.Null);
        }

        [Test]
        public void TemplateFolder_JsonRoundTrip_PreservesShouldApplyExpression()
        {
            var folder = new TemplateFolder
            {
                FolderPath = "MariaDB Only",
                QuenchSlot = TemplateQuenchSlot.Before,
                ShouldApplyExpression = "SELECT CASE WHEN @@version LIKE '%MariaDB%' THEN 1 ELSE 0 END"
            };

            var json = JsonConvert.SerializeObject(folder);
            var deserialized = JsonConvert.DeserializeObject<TemplateFolder>(json);

            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT CASE WHEN @@version LIKE '%MariaDB%' THEN 1 ELSE 0 END"));
        }

        [Test]
        public void ProductFolder_JsonRoundTrip_PreservesShouldApplyExpression()
        {
            var folder = new ProductFolder
            {
                FolderPath = "Jobs",
                QuenchSlot = ProductQuenchSlot.After,
                ShouldApplyExpression = "SELECT CASE WHEN SERVERPROPERTY('EngineEdition') <> 5 THEN 1 ELSE 0 END"
            };

            var json = JsonConvert.SerializeObject(folder);
            var deserialized = JsonConvert.DeserializeObject<ProductFolder>(json);

            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT CASE WHEN SERVERPROPERTY('EngineEdition') <> 5 THEN 1 ELSE 0 END"));
        }

        [Test]
        public void ScriptFolder_Serialize_WithNullShouldApplyExpression_OmitsIt()
        {
            var folder = new TemplateFolder { FolderPath = "Before Scripts", QuenchSlot = TemplateQuenchSlot.Before };

            var json = JsonHelper.Serialize(folder);

            Assert.That(json, Does.Not.Contain("ShouldApplyExpression"));
        }

        [Test]
        public void TemplateFolder_Clone_PreservesShouldApplyExpression()
        {
            var original = new TemplateFolder
            {
                FolderPath = "Region EU",
                QuenchSlot = TemplateQuenchSlot.Before,
                ShouldApplyExpression = "SELECT 1"
            };

            var clone = original.Clone();

            Assert.That(clone.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

        // ---- ShouldApplyExpression script-token resolution (#260 bug: gate expressions skipped the token pass) ----

        private static string NonexistentBasePath() =>
            Path.Combine(Path.GetTempPath(), "ss-folder-gate-" + Guid.NewGuid().ToString("N"));

        [Test]
        public void LoadSqlFiles_ResolvesScriptTokensInShouldApplyExpression()
        {
            var folder = new TemplateFolder
            {
                FolderPath = "EnvGated",
                QuenchSlot = TemplateQuenchSlot.Before,
                ShouldApplyExpression = "'{{EnvType}}' = 'prod'"
            };

            folder.LoadSqlFiles(NonexistentBasePath(), [new KeyValuePair<string, string>("EnvType", "prod")]);

            Assert.That(folder.ShouldApplyExpression, Is.EqualTo("'prod' = 'prod'"));
        }

        [Test]
        public void LoadSqlFiles_ResolvesScriptTokensInShouldApplyExpression_OnProductFolder()
        {
            var folder = new ProductFolder
            {
                FolderPath = "EnvGated",
                QuenchSlot = ProductQuenchSlot.Before,
                ShouldApplyExpression = "'{{EnvType}}' = 'prod'"
            };

            folder.LoadSqlFiles(NonexistentBasePath(), [new KeyValuePair<string, string>("EnvType", "prod")]);

            Assert.That(folder.ShouldApplyExpression, Is.EqualTo("'prod' = 'prod'"));
        }

        [Test]
        public void LoadSqlFiles_WithNullShouldApplyExpression_LeavesItNull()
        {
            var folder = new TemplateFolder { FolderPath = "Plain", QuenchSlot = TemplateQuenchSlot.Before };

            folder.LoadSqlFiles(NonexistentBasePath(), [new KeyValuePair<string, string>("EnvType", "prod")]);

            Assert.That(folder.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void LoadSqlFiles_LeavesSchemaNameTokenForPerIterationSubstitution()
        {
            // SchemaName is per-iteration (resolved at quench time by DatabaseQuench), so it must NOT
            // be in the load-time token set and must survive this pass intact.
            var folder = new TemplateFolder
            {
                FolderPath = "SchemaScoped",
                QuenchSlot = TemplateQuenchSlot.Before,
                ShouldApplyExpression = "'{{SchemaName}}' = 'tenant_a'"
            };

            folder.LoadSqlFiles(NonexistentBasePath(), [new KeyValuePair<string, string>("EnvType", "prod")]);

            Assert.That(folder.ShouldApplyExpression, Is.EqualTo("'{{SchemaName}}' = 'tenant_a'"));
        }

        [Test]
        public void LoadSqlFiles_WithNoTokensInShouldApplyExpression_LeavesItUnchanged()
        {
            var folder = new TemplateFolder
            {
                FolderPath = "Plain",
                QuenchSlot = TemplateQuenchSlot.Before,
                ShouldApplyExpression = "SELECT CASE WHEN @@version LIKE '%MariaDB%' THEN 1 ELSE 0 END"
            };

            folder.LoadSqlFiles(NonexistentBasePath(), [new KeyValuePair<string, string>("EnvType", "prod")]);

            Assert.That(folder.ShouldApplyExpression, Is.EqualTo("SELECT CASE WHEN @@version LIKE '%MariaDB%' THEN 1 ELSE 0 END"));
        }
    }
}

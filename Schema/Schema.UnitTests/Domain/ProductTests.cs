// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Utility;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class ProductTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var product = new Product();

            Assert.That(product.Name, Is.EqualTo(""));
            Assert.That(product.ValidationScript, Is.Null);
            Assert.That(product.TemplateOrder, Is.Not.Null);
            Assert.That(product.TemplateOrder, Is.Empty);
            Assert.That(product.ScriptTokens, Is.Not.Null);
            Assert.That(product.ScriptTokens, Is.Empty);
            Assert.That(product.ScriptFolders, Is.Not.Null);
            Assert.That(product.ScriptFolders, Is.Empty);
            Assert.That(product.BranchNameFile, Is.EqualTo("{{repo_path}}/.git/HEAD"));
            Assert.That(product.BeforeBranchNameMask, Is.EqualTo("ref: refs/heads/"));
            Assert.That(product.AfterBranchNameMask, Is.EqualTo(""));
            Assert.That(product.DropUnknownIndexes, Is.Null);
            Assert.That(product.BaselineValidationScript, Is.Null);
            Assert.That(product.VersionStampScript, Is.Null);
            Assert.That(product.MinimumVersion, Is.Null);
            Assert.That(product.FilePath, Is.EqualTo(""));
        }

        [Test]
        public void Platform_SerializesAsCanonicalString()
        {
            var product = new Product { Name = "Test", Platform = Platform.SqlServer };

            var json = JsonConvert.SerializeObject(product);

            Assert.That(json, Does.Contain("\"SqlServer\""));
        }

        [Test]
        public void Platform_DeserializesMSSQLAlias()
        {
            var json = "{\"Name\":\"Test\",\"Platform\":\"MSSQL\"}";

            var product = JsonConvert.DeserializeObject<Product>(json);

            Assert.That(product.Platform, Is.EqualTo(Platform.SqlServer));
        }

        [Test]
        public void Platform_DeserializesPostgreSQL()
        {
            var json = "{\"Name\":\"Test\",\"Platform\":\"PostgreSQL\"}";

            var product = JsonConvert.DeserializeObject<Product>(json);

            Assert.That(product.Platform, Is.EqualTo(Platform.PostgreSQL));
        }

        [Test]
        public void BeforeFolders_FiltersCorrectly()
        {
            var product = new Product
            {
                ScriptFolders = new List<ProductFolder>
                {
                    new ProductFolder { FolderPath = "before1", QuenchSlot = ProductQuenchSlot.Before },
                    new ProductFolder { FolderPath = "after1", QuenchSlot = ProductQuenchSlot.After },
                    new ProductFolder { FolderPath = "before2", QuenchSlot = ProductQuenchSlot.Before }
                }
            };

            Assert.That(product.BeforeFolders, Has.Count.EqualTo(2));
            Assert.That(product.BeforeFolders[0].FolderPath, Is.EqualTo("before1"));
            Assert.That(product.BeforeFolders[1].FolderPath, Is.EqualTo("before2"));
        }

        [Test]
        public void AfterFolders_FiltersCorrectly()
        {
            var product = new Product
            {
                ScriptFolders = new List<ProductFolder>
                {
                    new ProductFolder { FolderPath = "before1", QuenchSlot = ProductQuenchSlot.Before },
                    new ProductFolder { FolderPath = "after1", QuenchSlot = ProductQuenchSlot.After },
                    new ProductFolder { FolderPath = "after2", QuenchSlot = ProductQuenchSlot.After }
                }
            };

            Assert.That(product.AfterFolders, Has.Count.EqualTo(2));
            Assert.That(product.AfterFolders[0].FolderPath, Is.EqualTo("after1"));
            Assert.That(product.AfterFolders[1].FolderPath, Is.EqualTo("after2"));
        }

        [Test]
        public void BeforeFolders_ReturnsEmpty_WhenNoFolders()
        {
            var product = new Product();

            Assert.That(product.BeforeFolders, Is.Empty);
        }

        [Test]
        public void AfterFolders_ReturnsEmpty_WhenScriptFoldersIsNull()
        {
            var product = new Product { ScriptFolders = null };

            Assert.That(product.AfterFolders, Is.Empty);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var product = new Product
            {
                Name = "TestProduct",
                ValidationScript = "SELECT 1",
                Platform = Platform.MySQL,
                MinimumVersion = "8.0",
                TemplateOrder = new List<string> { "Template1", "Template2" },
                ScriptTokens = new Dictionary<string, string> { { "Token1", "Value1" } },
                DropUnknownIndexes = true,
                ScriptFolders = new List<ProductFolder>
                {
                    new ProductFolder { FolderPath = "Scripts", QuenchSlot = ProductQuenchSlot.Before }
                }
            };

            var json = JsonConvert.SerializeObject(product);
            var deserialized = JsonConvert.DeserializeObject<Product>(json);

            Assert.That(deserialized.Name, Is.EqualTo("TestProduct"));
            Assert.That(deserialized.ValidationScript, Is.EqualTo("SELECT 1"));
            Assert.That(deserialized.Platform, Is.EqualTo(Platform.MySQL));
            Assert.That(deserialized.MinimumVersion, Is.EqualTo("8.0"));
            Assert.That(deserialized.TemplateOrder, Has.Count.EqualTo(2));
            Assert.That(deserialized.ScriptTokens["Token1"], Is.EqualTo("Value1"));
            Assert.That(deserialized.DropUnknownIndexes, Is.True);
            Assert.That(deserialized.ScriptFolders, Has.Count.EqualTo(1));
            Assert.That(deserialized.ScriptFolders[0].QuenchSlot, Is.EqualTo(ProductQuenchSlot.Before));
        }

        [Test]
        public void ProductFolder_ServerToQuench_DefaultIsPrimary()
        {
            var folder = new ProductFolder();
            Assert.That(folder.ServerToQuench, Is.EqualTo(ServerToQuench.Primary));
        }

        [TestCase(ServerToQuench.Primary, true, false)]
        [TestCase(ServerToQuench.Secondary, false, true)]
        [TestCase(ServerToQuench.Both, true, true)]
        public void ProductFolder_QuenchOnPrimary_QuenchOnSecondary_CorrectForServerToQuench(
            ServerToQuench stq, bool expectedPrimary, bool expectedSecondary)
        {
            var folder = new ProductFolder { ServerToQuench = stq };
            Assert.That(folder.QuenchOnPrimary, Is.EqualTo(expectedPrimary));
            Assert.That(folder.QuenchOnSecondary, Is.EqualTo(expectedSecondary));
        }

        [Test]
        public void ProductFolder_ServerToQuench_SerializesAsString()
        {
            var folder = new ProductFolder
            {
                FolderPath = "Scripts",
                ServerToQuench = ServerToQuench.Both,
                QuenchSlot = ProductQuenchSlot.Before
            };

            var json = JsonConvert.SerializeObject(folder);
            Assert.That(json, Does.Contain("\"Both\""));
            Assert.That(json, Does.Not.Contain("QuenchOnPrimary"));
            Assert.That(json, Does.Not.Contain("QuenchOnSecondary"));
        }

        [Test]
        public void ProductFolder_JsonRoundTrip_PreservesServerToQuench()
        {
            var folder = new ProductFolder
            {
                FolderPath = "Scripts",
                ServerToQuench = ServerToQuench.Secondary,
                QuenchSlot = ProductQuenchSlot.After
            };

            var json = JsonConvert.SerializeObject(folder);
            var deserialized = JsonConvert.DeserializeObject<ProductFolder>(json);

            Assert.That(deserialized.ServerToQuench, Is.EqualTo(ServerToQuench.Secondary));
            Assert.That(deserialized.QuenchSlot, Is.EqualTo(ProductQuenchSlot.After));
        }

        [Test]
        public void CheckConstraintStyle_DefaultIsColumnLevel()
        {
            var product = new Product();
            Assert.That(product.CheckConstraintStyle, Is.EqualTo(CheckConstraintStyle.ColumnLevel));
        }

        [Test]
        public void CheckConstraintStyle_Serialize_WithColumnLevel_OmitsProperty()
        {
            var product = new Product { Name = "Test", CheckConstraintStyle = CheckConstraintStyle.ColumnLevel };

            var json = JsonHelper.Serialize(product);

            Assert.That(json, Does.Not.Contain("CheckConstraintStyle"));
        }

        [Test]
        public void CheckConstraintStyle_Serialize_WithTableLevel_IncludesProperty()
        {
            var product = new Product { Name = "Test", CheckConstraintStyle = CheckConstraintStyle.TableLevel };

            var json = JsonHelper.Serialize(product);

            Assert.That(json, Does.Contain("\"CheckConstraintStyle\""));
            Assert.That(json, Does.Contain("\"TableLevel\""));
        }

        [Test]
        public void CheckConstraintStyle_JsonRoundTrip_PreservesTableLevel()
        {
            var product = new Product { Name = "Test", Platform = Platform.SqlServer, CheckConstraintStyle = CheckConstraintStyle.TableLevel };

            var json = JsonConvert.SerializeObject(product);
            var deserialized = JsonConvert.DeserializeObject<Product>(json);

            Assert.That(deserialized.CheckConstraintStyle, Is.EqualTo(CheckConstraintStyle.TableLevel));
        }

        [Test]
        public void DropTablesRemovedFromProduct_AbsentInJson_DefaultsTrue()
        {
            var product = Newtonsoft.Json.JsonConvert.DeserializeObject<Schema.Domain.Product>(
                """{ "Name": "P", "Platform": "SqlServer" }""");
            Assert.That(product.DropTablesRemovedFromProduct, Is.True);
        }

        [Test]
        public void DropTablesRemovedFromProduct_ExplicitFalse_IsFalse()
        {
            var product = Newtonsoft.Json.JsonConvert.DeserializeObject<Schema.Domain.Product>(
                """{ "Name": "P", "Platform": "SqlServer", "DropTablesRemovedFromProduct": false }""");
            Assert.That(product.DropTablesRemovedFromProduct, Is.False);
        }

        [Test]
        public void DropTablesRemovedFromProduct_ExplicitTrue_IsTrue()
        {
            var product = Newtonsoft.Json.JsonConvert.DeserializeObject<Schema.Domain.Product>(
                """{ "Name": "P", "Platform": "SqlServer", "DropTablesRemovedFromProduct": true }""");
            Assert.That(product.DropTablesRemovedFromProduct, Is.True);
        }

        [Test]
        public void DropUnknownIndexes_AbsentInJson_IsNull()
        {
            var product = Newtonsoft.Json.JsonConvert.DeserializeObject<Schema.Domain.Product>(
                """{ "Name": "P", "Platform": "SqlServer" }""");
            Assert.That(product.DropUnknownIndexes, Is.Null);
        }

        [Test]
        public void DropUnknownIndexes_ExplicitFalse_IsFalse()
        {
            var product = Newtonsoft.Json.JsonConvert.DeserializeObject<Schema.Domain.Product>(
                """{ "Name": "P", "Platform": "SqlServer", "DropUnknownIndexes": false }""");
            Assert.That(product.DropUnknownIndexes, Is.False);
        }

        [Test]
        public void DropUnknownIndexes_ExplicitTrue_IsTrue()
        {
            var product = Newtonsoft.Json.JsonConvert.DeserializeObject<Schema.Domain.Product>(
                """{ "Name": "P", "Platform": "SqlServer", "DropUnknownIndexes": true }""");
            Assert.That(product.DropUnknownIndexes, Is.True);
        }
    }
}

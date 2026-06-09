// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
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
                ShouldApplyExpression = "SELECT 1",
                VariantName = "East catalog"
            };

            var json = JsonConvert.SerializeObject(fti);
            var deserialized = JsonConvert.DeserializeObject<FullTextIndex>(json);

            Assert.That(deserialized.FullTextCatalog, Is.EqualTo("FTC_Main"));
            Assert.That(deserialized.KeyIndex, Is.EqualTo("PK_Docs"));
            Assert.That(deserialized.ChangeTracking, Is.EqualTo("MANUAL"));
            Assert.That(deserialized.StopList, Is.EqualTo("MyStopList"));
            Assert.That(deserialized.Columns, Is.EqualTo("Title, Body"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
            Assert.That(deserialized.VariantName, Is.EqualTo("East catalog"));
        }

        [Test]
        public void JsonSerialization_OmitsNullProperties()
        {
            var fti = new FullTextIndex { FullTextCatalog = "FTC", KeyIndex = "PK", Columns = "Col1" };
            var json = JsonConvert.SerializeObject(fti, NullIgnore);

            Assert.That(json, Does.Not.Contain("ShouldApplyExpression"));
        }

        [Test]
        public void FullTextIndex_SingleObjectJson_DeserializesAsOneElementList()
        {
            var json = "{ \"Name\": \"[Docs]\", \"FullTextIndex\": { \"FullTextCatalog\": \"[FT_Catalog]\", \"KeyIndex\": \"[PK_Docs]\", \"Columns\": \"[Title]\" } }";
            var table = JsonConvert.DeserializeObject<SqlServerTable>(json);
            Assert.That(table.FullTextIndex, Has.Count.EqualTo(1));
            Assert.That(table.FullTextIndex[0].FullTextCatalog, Is.EqualTo("[FT_Catalog]"));
        }

        [Test]
        public void FullTextIndex_ArrayJson_DeserializesAsVariantList()
        {
            var json = "{ \"Name\": \"[Docs]\", \"FullTextIndex\": [ { \"FullTextCatalog\": \"[FT_Catalog]\", \"KeyIndex\": \"[PK_Docs]\", \"Columns\": \"[Title]\", \"ShouldApplyExpression\": \"DB_NAME() = 'East'\" }, { \"FullTextCatalog\": \"[FT_Catalog2]\", \"KeyIndex\": \"[PK_Docs]\", \"Columns\": \"[Title]\", \"ShouldApplyExpression\": \"DB_NAME() = 'West'\" } ] }";
            var table = JsonConvert.DeserializeObject<SqlServerTable>(json);
            Assert.That(table.FullTextIndex, Has.Count.EqualTo(2));
            Assert.That(table.FullTextIndex[1].FullTextCatalog, Is.EqualTo("[FT_Catalog2]"));
        }

        [Test]
        public void FullTextIndex_MultipleVariantsMissingExpressions_ThrowsAtLoad()
        {
            var json = "{ \"Name\": \"[Docs]\", \"FullTextIndex\": [ { \"FullTextCatalog\": \"[FT_Catalog]\", \"KeyIndex\": \"[PK_Docs]\", \"Columns\": \"[Title]\", \"ShouldApplyExpression\": \"DB_NAME() = 'East'\" }, { \"FullTextCatalog\": \"[FT_Catalog2]\", \"KeyIndex\": \"[PK_Docs]\", \"Columns\": \"[Title]\" } ] }";
            var ex = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<SqlServerTable>(json));
            Assert.That(ex.Message, Does.Contain("ShouldApplyExpression on every variant"));
        }

        [Test]
        public void FullTextIndex_SingleVariant_SerializesAsBareObject()
        {
            var table = new SqlServerTable { Name = "[Docs]", FullTextIndex = [new FullTextIndex { FullTextCatalog = "[FT]", KeyIndex = "[PK]", Columns = "[Title]" }] };
            var json = JsonConvert.SerializeObject(table);
            Assert.That(json, Does.Contain("\"FullTextIndex\":{"));
        }

        [Test]
        public void FullTextIndex_TwoVariants_SerializeAsArray()
        {
            var table = new SqlServerTable
            {
                Name = "[Docs]",
                FullTextIndex =
                [
                    new FullTextIndex { FullTextCatalog = "[A]", KeyIndex = "[PK]", Columns = "[T]", ShouldApplyExpression = "1 = 1" },
                    new FullTextIndex { FullTextCatalog = "[B]", KeyIndex = "[PK]", Columns = "[T]", ShouldApplyExpression = "1 = 0" }
                ]
            };
            var json = JsonConvert.SerializeObject(table);
            Assert.That(json, Does.Contain("\"FullTextIndex\":["));
        }

        [Test]
        public void FullTextIndex_EmptyList_OmittedFromJson()
        {
            var table = new SqlServerTable { Name = "[Docs]" };
            var json = JsonConvert.SerializeObject(table);
            Assert.That(json, Does.Not.Contain("FullTextIndex"));
        }

        [Test]
        public void FullTextIndex_NullVariantEntry_ThrowsAtLoad()
        {
            var json = "{ \"Name\": \"[Docs]\", \"FullTextIndex\": [ null, { \"FullTextCatalog\": \"[FT]\", \"KeyIndex\": \"[PK]\", \"Columns\": \"[T]\", \"ShouldApplyExpression\": \"1 = 1\" } ] }";
            var ex = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<SqlServerTable>(json));
            Assert.That(ex.Message, Does.Contain("cannot be null"));
        }

        [Test]
        public void FullTextIndex_NullList_WritesJsonNull()
        {
            var converter = new FullTextIndexListJsonConverter();
            var sw = new StringWriter();
            using var writer = new JsonTextWriter(sw);
            converter.WriteJson(writer, null, JsonSerializer.CreateDefault());
            Assert.That(sw.ToString(), Is.EqualTo("null"));
        }

        [Test]
        public void FullTextIndex_VariantArrayJson_RoundTripsVariantNames()
        {
            var json = @"[{""FullTextCatalog"":""A"",""KeyIndex"":""PK"",""Columns"":""T"",""ShouldApplyExpression"":""DB_NAME() = 'East'"",""VariantName"":""East""},
                          {""FullTextCatalog"":""B"",""KeyIndex"":""PK"",""Columns"":""T"",""ShouldApplyExpression"":""DB_NAME() = 'West'"",""VariantName"":""West""}]";
            var table = JsonConvert.DeserializeObject<SqlServerTable>($@"{{""Name"":""Docs"",""FullTextIndex"":{json}}}");
            Assert.That(table.FullTextIndex[0].VariantName, Is.EqualTo("East"));
            Assert.That(table.FullTextIndex[1].VariantName, Is.EqualTo("West"));
            var serialized = JsonConvert.SerializeObject(table);
            Assert.That(serialized, Does.Contain(@"""VariantName"":""West"""));
        }
    }
}

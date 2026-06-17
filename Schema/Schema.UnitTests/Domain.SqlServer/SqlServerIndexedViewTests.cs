// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Utility;
using System;
using System.Collections.Generic;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class SqlServerIndexedViewTests
    {
        [Test]
        public void InheritsFromDynamicBase()
        {
            var view = new SqlServerIndexedView();
            Assert.That(view, Is.InstanceOf<DynamicBase>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var view = new SqlServerIndexedView();
            Assert.Multiple(() =>
            {
                // Slice 1 (Schema Templates): bare-constructor Schema is null;
                // SchemaDefaultResolver fills it. See SchemaDefaultResolverTests.
                Assert.That(view.Schema, Is.Null);
                Assert.That(view.Name, Is.EqualTo(""));
                Assert.That(view.Definition, Is.EqualTo(""));
                Assert.That(view.ShouldApplyExpression, Is.Null);
                Assert.That(view.Indexes, Is.Not.Null);
                Assert.That(view.Indexes, Is.Empty);
            });
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var view = new SqlServerIndexedView
            {
                Schema = "[sales]",
                Name = "[vOrderSummary]",
                Definition = "SELECT o.CustomerID, COUNT_BIG(*) AS OrderCount\nFROM Sales.SalesOrderHeader o\nGROUP BY o.CustomerID",
                ShouldApplyExpression = "{{IsProduction}}",
                Indexes =
                [
                    new SqlServerIndex
                    {
                        Name = "[IX_vOrderSummary_Clustered]",
                        Unique = true,
                        Clustered = true,
                        IndexColumns = "[CustomerID]"
                    },
                    new SqlServerIndex
                    {
                        Name = "[IX_vOrderSummary_OrderCount]",
                        Unique = false,
                        Clustered = false,
                        IndexColumns = "[OrderCount]",
                        CompressionType = "PAGE"
                    }
                ]
            };

            var json = JsonHelper.Serialize(view);
            var deserialized = JsonConvert.DeserializeObject<SqlServerIndexedView>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Schema, Is.EqualTo("[sales]"));
                Assert.That(deserialized.Name, Is.EqualTo("[vOrderSummary]"));
                Assert.That(deserialized.Definition, Does.Contain("COUNT_BIG"));
                Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("{{IsProduction}}"));
                Assert.That(deserialized.Indexes, Has.Count.EqualTo(2));
                Assert.That(deserialized.Indexes[0].Clustered, Is.True);
                Assert.That(deserialized.Indexes[0].Unique, Is.True);
                Assert.That(deserialized.Indexes[1].CompressionType, Is.EqualTo("PAGE"));
            });
        }

        [Test]
        public void CustomProperties_SurviveRoundTrip()
        {
            var view = new SqlServerIndexedView { Name = "[vTest]" };
            view.Extensions = new JObject { ["CustomFlag"] = true };

            var json = JsonHelper.Serialize(view);
            var deserialized = JsonConvert.DeserializeObject<SqlServerIndexedView>(json);

            Assert.That(deserialized.Extensions?["CustomFlag"], Is.Not.Null);
        }

        [Test]
        public void DefaultValueHandling_OmitsDefaults()
        {
            var view = new SqlServerIndexedView { Name = "[vTest]" };
            var json = JsonHelper.Serialize(view);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"Name\""));
                Assert.That(json, Does.Not.Contain("\"ShouldApplyExpression\""));
            });
        }

        [Test]
        public void DeserializeIndexedView_RoundTrips()
        {
            var view = new SqlServerIndexedView
            {
                Schema = "[dbo]",
                Name = "[vTest]",
                Definition = "SELECT 1 AS Col1",
                Indexes = [new SqlServerIndex { Name = "[IX_Clustered]", Unique = true, Clustered = true, IndexColumns = "[Col1]" }]
            };
            var json = JsonHelper.Serialize(view);
            var result = PlatformDeserializer.DeserializeIndexedView(json, Platform.SqlServer);

            Assert.Multiple(() =>
            {
                Assert.That(result.Name, Is.EqualTo("[vTest]"));
                Assert.That(result.Indexes, Has.Count.EqualTo(1));
                Assert.That(result.Indexes[0].Clustered, Is.True);
            });
        }

        [Test]
        public void DeserializeIndexedView_ThrowsForNonSqlServer()
        {
            Assert.Throws<ArgumentException>(() =>
                PlatformDeserializer.DeserializeIndexedView("{}", Platform.PostgreSQL));
        }

        [Test]
        public void JsonRoundTrip_PreservesVariantName()
        {
            var view = new SqlServerIndexedView { Name = "[vTest]", Definition = "SELECT 1 AS Col1", ShouldApplyExpression = "1=1", VariantName = "EU region" };
            var json = JsonConvert.SerializeObject(view);
            var deserialized = JsonConvert.DeserializeObject<SqlServerIndexedView>(json);
            Assert.That(deserialized.VariantName, Is.EqualTo("EU region"));
        }
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Newtonsoft.Json;
using Schema.Domain;

using SchemaSmith.Pro;
namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class TableTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var table = new Table();

            Assert.That(table.Name, Is.EqualTo(""));
            Assert.That(table.Columns, Is.Not.Null);
            Assert.That(table.Columns, Is.Empty);
            Assert.That(table.Indexes, Is.Not.Null);
            Assert.That(table.Indexes, Is.Empty);
            Assert.That(table.ForeignKeys, Is.Not.Null);
            Assert.That(table.ForeignKeys, Is.Empty);
            Assert.That(table.CheckConstraints, Is.Not.Null);
            Assert.That(table.CheckConstraints, Is.Empty);
            Assert.That(table.ShouldApplyExpression, Is.Null);
            Assert.That(table.DataDelivery, Is.Null);
            Assert.That(table.OldName, Is.Null);
        }

        [Test]
        public void DataDelivery_MergeType_AcceptsStandardizedValues()
        {
            var table = new Table();

            table.DataDelivery = new DataDelivery { MergeType = "None" };
            Assert.That(table.DataDelivery.MergeType, Is.EqualTo("None"));

            table.DataDelivery = new DataDelivery { MergeType = "Insert" };
            Assert.That(table.DataDelivery.MergeType, Is.EqualTo("Insert"));

            table.DataDelivery = new DataDelivery { MergeType = "Insert/Update" };
            Assert.That(table.DataDelivery.MergeType, Is.EqualTo("Insert/Update"));

            table.DataDelivery = new DataDelivery { MergeType = "Insert/Update/Delete" };
            Assert.That(table.DataDelivery.MergeType, Is.EqualTo("Insert/Update/Delete"));
        }

        [Test]
        public void JsonRoundTrip_WithNestedObjects()
        {
            var table = new Table
            {
                Name = "Customer",
                DataDelivery = new DataDelivery { MergeType = "Insert/Update", MatchColumns = "Id" },
                Columns = new List<Column>
                {
                    new Column { Name = "Id", DataType = "int", Nullable = false },
                    new Column { Name = "Name", DataType = "varchar(100)", Nullable = true }
                },
                Indexes = new List<Index>
                {
                    new Index { Name = "PK_Customer", PrimaryKey = true, IndexColumns = "Id ASC" }
                },
                ForeignKeys = new List<ForeignKey>
                {
                    new ForeignKey { Name = "FK_Customer_Region", Columns = "RegionId", RelatedTable = "Region", RelatedColumns = "Id" }
                },
                CheckConstraints = new List<CheckConstraint>
                {
                    new CheckConstraint { Name = "CK_Customer_Name", Expression = "LEN(Name) > 0" }
                }
            };

            var json = JsonConvert.SerializeObject(table);
            var deserialized = JsonConvert.DeserializeObject<Table>(json);

            Assert.That(deserialized.Name, Is.EqualTo("Customer"));
            Assert.That(deserialized.DataDelivery.MergeType, Is.EqualTo("Insert/Update"));
            Assert.That(deserialized.DataDelivery.MatchColumns, Is.EqualTo("Id"));
            Assert.That(deserialized.Columns, Has.Count.EqualTo(2));
            Assert.That(deserialized.Columns[0].Name, Is.EqualTo("Id"));
            Assert.That(deserialized.Columns[1].Nullable, Is.True);
            Assert.That(deserialized.Indexes, Has.Count.EqualTo(1));
            Assert.That(deserialized.Indexes[0].PrimaryKey, Is.True);
            Assert.That(deserialized.ForeignKeys, Has.Count.EqualTo(1));
            Assert.That(deserialized.ForeignKeys[0].RelatedTable, Is.EqualTo("Region"));
            Assert.That(deserialized.CheckConstraints, Has.Count.EqualTo(1));
            Assert.That(deserialized.CheckConstraints[0].Expression, Is.EqualTo("LEN(Name) > 0"));
        }

    }
}

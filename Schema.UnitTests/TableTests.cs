// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using NSubstitute;

using NSubstitute;
using Newtonsoft.Json;
using Schema.Isolators;
using Schema.Domain;
using System;

namespace Schema.UnitTests;

public class TableTests
{
    [Test]
    public void ShouldProvideTheFileNameWhenErrorLoadingATable()
    {
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => Table.Load("badPath"));
            Assert.That(ex!.Message, Contains.Substring("Error loading table from badPath"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldSerializeUpdateFillFactor()
    {
        var table = new Table { Name = "Test", UpdateFillFactor = true };
        var json = JsonConvert.SerializeObject(table);
        Assert.That(json, Contains.Substring("\"UpdateFillFactor\":true"));
    }

    [Test]
    public void ShouldDeserializeUpdateFillFactorDefaultsToFalse()
    {
        var json = "{\"Name\":\"Test\"}";
        var table = JsonConvert.DeserializeObject<Table>(json);
        Assert.That(table!.UpdateFillFactor, Is.False);
    }

    [Test]
    public void DefaultValues_AreCorrect()
    {
        var table = new Table();
        Assert.Multiple(() =>
        {
            Assert.That(table.Schema, Is.EqualTo("dbo"));
            Assert.That(table.Name, Is.Null);
            Assert.That(table.CompressionType, Is.EqualTo("NONE"));
            Assert.That(table.IsTemporal, Is.False);
            Assert.That(table.Columns, Is.Not.Null.And.Empty);
            Assert.That(table.Indexes, Is.Not.Null.And.Empty);
            Assert.That(table.XmlIndexes, Is.Not.Null.And.Empty);
            Assert.That(table.ForeignKeys, Is.Not.Null.And.Empty);
            Assert.That(table.CheckConstraints, Is.Not.Null.And.Empty);
            Assert.That(table.Statistics, Is.Not.Null.And.Empty);
            Assert.That(table.FullTextIndex, Is.Null);
            Assert.That(table.OldName, Is.EqualTo(""));
            Assert.That(table.UpdateFillFactor, Is.False);
        });
    }

    [Test]
    public void JsonRoundTrip_PreservesAllCollections()
    {
        var original = new Table
        {
            Schema = "sales",
            Name = "Orders",
            CompressionType = "PAGE",
            IsTemporal = true,
            UpdateFillFactor = true,
            OldName = "OldOrders",
            Columns = [new Column { Name = "OrderId", DataType = "INT" }],
            Indexes = [new Schema.Domain.Index { Name = "PK_Orders", PrimaryKey = true, IndexColumns = "OrderId" }],
            XmlIndexes = [new XmlIndex { Name = "PXML_Data", IsPrimary = true, Column = "XmlCol" }],
            ForeignKeys = [new ForeignKey { Name = "FK_Test", Columns = "CustId", RelatedTable = "Customer", RelatedColumns = "Id" }],
            CheckConstraints = [new CheckConstraint { Name = "CK_Status", Expression = "[Status] > 0" }],
            Statistics = [new Statistic { Name = "ST_Date", Columns = "OrderDate" }],
            FullTextIndex = new FullTextIndex { FullTextCatalog = "FT_Cat", KeyIndex = "PK_Orders", Columns = "Notes" }
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<Table>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized!.Schema, Is.EqualTo("sales"));
            Assert.That(deserialized.Name, Is.EqualTo("Orders"));
            Assert.That(deserialized.CompressionType, Is.EqualTo("PAGE"));
            Assert.That(deserialized.IsTemporal, Is.True);
            Assert.That(deserialized.UpdateFillFactor, Is.True);
            Assert.That(deserialized.OldName, Is.EqualTo("OldOrders"));
            Assert.That(deserialized.Columns, Has.Count.EqualTo(1));
            Assert.That(deserialized.Indexes, Has.Count.EqualTo(1));
            Assert.That(deserialized.XmlIndexes, Has.Count.EqualTo(1));
            Assert.That(deserialized.ForeignKeys, Has.Count.EqualTo(1));
            Assert.That(deserialized.CheckConstraints, Has.Count.EqualTo(1));
            Assert.That(deserialized.Statistics, Has.Count.EqualTo(1));
            Assert.That(deserialized.FullTextIndex, Is.Not.Null);
            Assert.That(deserialized.FullTextIndex.FullTextCatalog, Is.EqualTo("FT_Cat"));
        });
    }
}

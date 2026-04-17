// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Schema.Delivery;

namespace Schema.UnitTests.Delivery;

[TestFixture]
public class DataDeliveryHelperTests
{
    private class TestColumn : IDeliverableColumn
    {
        public string Name { get; set; }
        public bool Nullable { get; set; }
    }

    private class TestFK : IDeliverableForeignKey
    {
        public string Columns { get; set; }
        public string RelatedTable { get; set; }
        public string RelatedTableSchema { get; set; }
    }

    private class TestTable : IDeliverableTable
    {
        public string Name { get; set; }
        public string Schema { get; set; }
        public DataDelivery DataDelivery { get; set; }
        public IReadOnlyList<IDeliverableColumn> DeliverableColumns { get; set; } = new List<IDeliverableColumn>();
        public IReadOnlyList<IDeliverableForeignKey> DeliverableForeignKeys { get; set; } = new List<IDeliverableForeignKey>();
    }

    [Test]
    public void BuildDeliveryTableSet_SqlServer_IncludesSchemaQualifiedKeys()
    {
        var tables = new List<IDeliverableTable>
        {
            new TestTable { Name = "[Users]", Schema = "dbo" },
            new TestTable { Name = "[Orders]", Schema = "sales" }
        };

        var set = DataDeliveryHelper.BuildDeliveryTableSet(tables, "SqlServer");

        Assert.That(set, Does.Contain("dbo.Users"));
        Assert.That(set, Does.Contain("sales.Orders"));
    }

    [Test]
    public void BuildDeliveryTableSet_MySQL_UsesTableNameOnly()
    {
        var tables = new List<IDeliverableTable>
        {
            new TestTable { Name = "`users`" },
            new TestTable { Name = "`orders`" }
        };

        var set = DataDeliveryHelper.BuildDeliveryTableSet(tables, "MySQL");

        Assert.That(set, Does.Contain("users"));
        Assert.That(set, Does.Contain("orders"));
    }

    [Test]
    public void ClassifyFKEdges_NotNullFK_RequiredDep()
    {
        var table = new TestTable
        {
            Name = "Orders", Schema = "dbo",
            DeliverableColumns = new List<IDeliverableColumn>
            {
                new TestColumn { Name = "CustomerId", Nullable = false }
            },
            DeliverableForeignKeys = new List<IDeliverableForeignKey>
            {
                new TestFK { Columns = "CustomerId", RelatedTable = "Customers", RelatedTableSchema = "dbo" }
            }
        };
        var deliverySet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "dbo.Customers" };

        var (required, deferred) = DataDeliveryHelper.ClassifyFKEdges(table, deliverySet, "SqlServer");

        Assert.That(required, Does.Contain("dbo.Customers"));
        Assert.That(deferred, Is.Empty);
    }

    [Test]
    public void ClassifyFKEdges_NullableFK_DeferredColumn()
    {
        var table = new TestTable
        {
            Name = "Orders", Schema = "dbo",
            DeliverableColumns = new List<IDeliverableColumn>
            {
                new TestColumn { Name = "ManagerId", Nullable = true }
            },
            DeliverableForeignKeys = new List<IDeliverableForeignKey>
            {
                new TestFK { Columns = "ManagerId", RelatedTable = "Employees", RelatedTableSchema = "dbo" }
            }
        };
        var deliverySet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "dbo.Employees" };

        var (required, deferred) = DataDeliveryHelper.ClassifyFKEdges(table, deliverySet, "SqlServer");

        Assert.That(required, Is.Empty);
        Assert.That(deferred, Does.Contain("ManagerId"));
    }

    [Test]
    public void ClassifyFKEdges_SelfReferencing_Skipped()
    {
        var table = new TestTable
        {
            Name = "Categories", Schema = "dbo",
            DeliverableColumns = new List<IDeliverableColumn>
            {
                new TestColumn { Name = "ParentId", Nullable = true }
            },
            DeliverableForeignKeys = new List<IDeliverableForeignKey>
            {
                new TestFK { Columns = "ParentId", RelatedTable = "Categories", RelatedTableSchema = "dbo" }
            }
        };
        var deliverySet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "dbo.Categories" };

        var (required, deferred) = DataDeliveryHelper.ClassifyFKEdges(table, deliverySet, "SqlServer");

        Assert.That(required, Is.Empty);
        Assert.That(deferred, Is.Empty);
    }

    [Test]
    public void ClassifyFKEdges_RelatedTableNotInDeliverySet_Skipped()
    {
        var table = new TestTable
        {
            Name = "Orders", Schema = "dbo",
            DeliverableColumns = new List<IDeliverableColumn>
            {
                new TestColumn { Name = "StatusId", Nullable = false }
            },
            DeliverableForeignKeys = new List<IDeliverableForeignKey>
            {
                new TestFK { Columns = "StatusId", RelatedTable = "Statuses", RelatedTableSchema = "dbo" }
            }
        };
        var deliverySet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        var (required, deferred) = DataDeliveryHelper.ClassifyFKEdges(table, deliverySet, "SqlServer");

        Assert.That(required, Is.Empty);
        Assert.That(deferred, Is.Empty);
    }

    [Test]
    public void TrimIdentifierQuotes_AllPlatforms()
    {
        Assert.That(DataDeliveryHelper.TrimIdentifierQuotes("[Users]", "SqlServer"), Is.EqualTo("Users"));
        Assert.That(DataDeliveryHelper.TrimIdentifierQuotes("\"users\"", "PostgreSQL"), Is.EqualTo("users"));
        Assert.That(DataDeliveryHelper.TrimIdentifierQuotes("`users`", "MySQL"), Is.EqualTo("users"));
    }

    [Test]
    public void GetDefaultSchema_ByPlatform()
    {
        Assert.That(DataDeliveryHelper.GetDefaultSchema("SqlServer"), Is.EqualTo("dbo"));
        Assert.That(DataDeliveryHelper.GetDefaultSchema("PostgreSQL"), Is.EqualTo("public"));
        Assert.That(DataDeliveryHelper.GetDefaultSchema("MySQL"), Is.EqualTo(""));
    }

    [Test]
    public void GetTableKey_SqlServer_SchemaQualified()
    {
        var table = new TestTable { Name = "Users", Schema = "dbo" };
        Assert.That(DataDeliveryHelper.GetTableKey(table, "SqlServer"), Is.EqualTo("dbo.Users"));
    }

    [Test]
    public void GetTableKey_MySQL_NameOnly()
    {
        var table = new TestTable { Name = "users" };
        Assert.That(DataDeliveryHelper.GetTableKey(table, "MySQL"), Is.EqualTo("users"));
    }
}

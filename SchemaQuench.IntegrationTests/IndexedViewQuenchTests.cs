using System;
using System.Data;
using Schema.DataAccess;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class IndexedViewQuenchTests : BaseTableQuenchTests
{
    private static void RunIndexedViewQuench(IDbCommand cmd, string productName, string json, bool whatIf = false)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = $"EXEC SchemaSmith.IndexedViewQuench @ProductName = '{productName}', @IndexedViewSchema = '{json.Replace("'", "''")}', @WhatIf = {(whatIf ? "1" : "0")}, @UpdateFillFactor = 0";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public void ShouldCreateIndexedViewWithClusteredAndNonclusteredIndexes()
    {
        const string product = "IV Create Test";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_Orders', 'U') IS NULL
    CREATE TABLE dbo.IVTest_Orders (OrderId INT NOT NULL, Status NVARCHAR(20), Amount DECIMAL(10,2))";
        cmd.ExecuteNonQuery();

        var json = @"[{
            ""Schema"": ""dbo"",
            ""Name"": ""vw_OrderSummary"",
            ""Definition"": ""SELECT OrderId, Status, Amount FROM dbo.IVTest_Orders"",
            ""Indexes"": [{
                ""Name"": ""CIX_vw_OrderSummary"",
                ""Unique"": true,
                ""Clustered"": true,
                ""IndexColumns"": ""OrderId""
            }, {
                ""Name"": ""IX_vw_OrderSummary_Status"",
                ""Unique"": false,
                ""Clustered"": false,
                ""IndexColumns"": ""Status""
            }]
        }]";

        RunIndexedViewQuench(cmd, product, json);

        // View exists
        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_OrderSummary')";
        Assert.That(cmd.ExecuteScalar(), Is.Not.Null.And.Not.EqualTo(DBNull.Value));

        // View is indexed
        cmd.CommandText = "SELECT OBJECTPROPERTY(OBJECT_ID('dbo.vw_OrderSummary'), 'IsIndexed')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(1));

        // Both indexes exist
        cmd.CommandText = "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.vw_OrderSummary') AND name = 'CIX_vw_OrderSummary'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("CIX_vw_OrderSummary"));

        cmd.CommandText = "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.vw_OrderSummary') AND name = 'IX_vw_OrderSummary_Status'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("IX_vw_OrderSummary_Status"));

        // Ownership extended property
        cmd.CommandText = "SELECT CAST(value AS NVARCHAR(200)) FROM sys.extended_properties WHERE major_id = OBJECT_ID('dbo.vw_OrderSummary') AND name = 'SchemaSmith_Product'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo(product));

        conn.Close();
    }

    [Test]
    public void ShouldRecreateViewWhenDefinitionChanges()
    {
        const string product = "IV Recreate Test";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_Products', 'U') IS NULL
    CREATE TABLE dbo.IVTest_Products (ProductId INT NOT NULL, Name NVARCHAR(100), Price DECIMAL(10,2))";
        cmd.ExecuteNonQuery();

        // First quench: view without Price
        var json1 = @"[{
            ""Schema"": ""dbo"",
            ""Name"": ""vw_ProductList"",
            ""Definition"": ""SELECT ProductId, Name FROM dbo.IVTest_Products"",
            ""Indexes"": [{
                ""Name"": ""CIX_vw_ProductList"",
                ""Unique"": true,
                ""Clustered"": true,
                ""IndexColumns"": ""ProductId""
            }]
        }]";

        RunIndexedViewQuench(cmd, product, json1);

        // Verify initial view exists
        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_ProductList')";
        Assert.That(cmd.ExecuteScalar(), Is.Not.Null.And.Not.EqualTo(DBNull.Value));

        // Second quench: view with Price added
        var json2 = @"[{
            ""Schema"": ""dbo"",
            ""Name"": ""vw_ProductList"",
            ""Definition"": ""SELECT ProductId, Name, Price FROM dbo.IVTest_Products"",
            ""Indexes"": [{
                ""Name"": ""CIX_vw_ProductList"",
                ""Unique"": true,
                ""Clustered"": true,
                ""IndexColumns"": ""ProductId""
            }]
        }]";

        RunIndexedViewQuench(cmd, product, json2);

        // Verify view definition now contains Price
        cmd.CommandText = "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID('dbo.vw_ProductList')";
        var definition = cmd.ExecuteScalar()?.ToString();
        Assert.That(definition, Does.Contain("Price"));

        conn.Close();
    }

    [Test]
    public void ShouldRemoveIndexedViewNotInSchema()
    {
        const string product = "IV Remove Test";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_Customers', 'U') IS NULL
    CREATE TABLE dbo.IVTest_Customers (CustomerId INT NOT NULL, Email NVARCHAR(200))";
        cmd.ExecuteNonQuery();

        // First quench: create an indexed view
        var json = @"[{
            ""Schema"": ""dbo"",
            ""Name"": ""vw_CustomerEmails"",
            ""Definition"": ""SELECT CustomerId, Email FROM dbo.IVTest_Customers"",
            ""Indexes"": [{
                ""Name"": ""CIX_vw_CustomerEmails"",
                ""Unique"": true,
                ""Clustered"": true,
                ""IndexColumns"": ""CustomerId""
            }]
        }]";

        RunIndexedViewQuench(cmd, product, json);

        // Verify view exists
        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_CustomerEmails')";
        Assert.That(cmd.ExecuteScalar(), Is.Not.Null.And.Not.EqualTo(DBNull.Value));

        // Second quench: empty array removes the view
        RunIndexedViewQuench(cmd, product, "[]");

        // Verify view no longer exists
        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_CustomerEmails')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(DBNull.Value).Or.Null);

        conn.Close();
    }

    [Test]
    public void ShouldNotCreateViewInWhatIfMode()
    {
        const string product = "IV WhatIf Test";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_WhatIf', 'U') IS NULL
    CREATE TABLE dbo.IVTest_WhatIf (Id INT NOT NULL, Val INT)";
        cmd.ExecuteNonQuery();

        var json = @"[{
            ""Schema"": ""dbo"",
            ""Name"": ""vw_WhatIfTest"",
            ""Definition"": ""SELECT Id, Val FROM dbo.IVTest_WhatIf"",
            ""Indexes"": [{
                ""Name"": ""CIX_vw_WhatIfTest"",
                ""Unique"": true,
                ""Clustered"": true,
                ""IndexColumns"": ""Id""
            }]
        }]";

        RunIndexedViewQuench(cmd, product, json, whatIf: true);

        // Verify view does NOT exist
        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_WhatIfTest')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(DBNull.Value).Or.Null);

        conn.Close();
    }

    [Test]
    public void ShouldPreserveViewOnIdempotentReQuench()
    {
        const string product = "IV Idempotent Test";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_Idempotent', 'U') IS NULL
    CREATE TABLE dbo.IVTest_Idempotent (Id INT NOT NULL, Name NVARCHAR(100))";
        cmd.ExecuteNonQuery();

        var json = @"[{
        ""Schema"": ""dbo"",
        ""Name"": ""vw_Idempotent"",
        ""Definition"": ""SELECT Id, Name FROM dbo.IVTest_Idempotent"",
        ""Indexes"": [{
            ""Name"": ""CIX_vw_Idempotent"",
            ""Unique"": true,
            ""Clustered"": true,
            ""IndexColumns"": ""Id""
        }]
    }]";

        RunIndexedViewQuench(cmd, product, json);

        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_Idempotent')";
        var firstObjectId = cmd.ExecuteScalar();

        RunIndexedViewQuench(cmd, product, json);

        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_Idempotent')";
        var secondObjectId = cmd.ExecuteScalar();
        Assert.That(secondObjectId, Is.EqualTo(firstObjectId), "Idempotent re-quench should not recreate the view");

        conn.Close();
    }

    [Test]
    public void ShouldAddIndexWithoutRecreatingView()
    {
        const string product = "IV IndexOnly Test";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_IndexOnly', 'U') IS NULL
    CREATE TABLE dbo.IVTest_IndexOnly (Id INT NOT NULL, Category NVARCHAR(50))";
        cmd.ExecuteNonQuery();

        var json1 = @"[{
        ""Schema"": ""dbo"",
        ""Name"": ""vw_IndexOnly"",
        ""Definition"": ""SELECT Id, Category FROM dbo.IVTest_IndexOnly"",
        ""Indexes"": [{
            ""Name"": ""CIX_vw_IndexOnly"",
            ""Unique"": true,
            ""Clustered"": true,
            ""IndexColumns"": ""Id""
        }]
    }]";

        RunIndexedViewQuench(cmd, product, json1);
        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_IndexOnly')";
        var firstObjectId = cmd.ExecuteScalar();

        var json2 = @"[{
        ""Schema"": ""dbo"",
        ""Name"": ""vw_IndexOnly"",
        ""Definition"": ""SELECT Id, Category FROM dbo.IVTest_IndexOnly"",
        ""Indexes"": [{
            ""Name"": ""CIX_vw_IndexOnly"",
            ""Unique"": true,
            ""Clustered"": true,
            ""IndexColumns"": ""Id""
        }, {
            ""Name"": ""IX_vw_IndexOnly_Category"",
            ""Unique"": false,
            ""Clustered"": false,
            ""IndexColumns"": ""Category""
        }]
    }]";

        RunIndexedViewQuench(cmd, product, json2);

        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_IndexOnly')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(firstObjectId), "Adding an index should not recreate the view");

        cmd.CommandText = "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.vw_IndexOnly') AND name = 'IX_vw_IndexOnly_Category'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("IX_vw_IndexOnly_Category"));

        conn.Close();
    }

    [Test]
    public void ShouldQuenchMultipleViewsIndependently()
    {
        const string product = "IV Multi Test";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_Multi', 'U') IS NULL
    CREATE TABLE dbo.IVTest_Multi (Id INT NOT NULL, Code NVARCHAR(10), Val INT)";
        cmd.ExecuteNonQuery();

        var json = @"[{
        ""Schema"": ""dbo"",
        ""Name"": ""vw_MultiA"",
        ""Definition"": ""SELECT Id, Code FROM dbo.IVTest_Multi"",
        ""Indexes"": [{
            ""Name"": ""CIX_vw_MultiA"",
            ""Unique"": true,
            ""Clustered"": true,
            ""IndexColumns"": ""Id""
        }]
    }, {
        ""Schema"": ""dbo"",
        ""Name"": ""vw_MultiB"",
        ""Definition"": ""SELECT Id, Val FROM dbo.IVTest_Multi"",
        ""Indexes"": [{
            ""Name"": ""CIX_vw_MultiB"",
            ""Unique"": true,
            ""Clustered"": true,
            ""IndexColumns"": ""Id""
        }]
    }]";

        RunIndexedViewQuench(cmd, product, json);

        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_MultiA')";
        Assert.That(cmd.ExecuteScalar(), Is.Not.Null.And.Not.EqualTo(DBNull.Value), "vw_MultiA should exist");

        cmd.CommandText = "SELECT OBJECT_ID('dbo.vw_MultiB')";
        Assert.That(cmd.ExecuteScalar(), Is.Not.Null.And.Not.EqualTo(DBNull.Value), "vw_MultiB should exist");

        conn.Close();
    }

    [Test]
    public void ShouldErrorWhenQuenchingViewOwnedByDifferentProduct()
    {
        const string originalProduct = "IV Ownership Test";
        const string wrongProduct = "WrongProduct";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
IF OBJECT_ID('dbo.IVTest_Ownership', 'U') IS NULL
    CREATE TABLE dbo.IVTest_Ownership (Id INT NOT NULL, Val INT)";
        cmd.ExecuteNonQuery();

        var json = @"[{
        ""Schema"": ""dbo"",
        ""Name"": ""vw_OwnershipTest"",
        ""Definition"": ""SELECT Id, Val FROM dbo.IVTest_Ownership"",
        ""Indexes"": [{
            ""Name"": ""CIX_vw_OwnershipTest"",
            ""Unique"": true,
            ""Clustered"": true,
            ""IndexColumns"": ""Id""
        }]
    }]";

        // First quench — creates view owned by originalProduct
        RunIndexedViewQuench(cmd, originalProduct, json);

        // Verify ownership EP exists
        cmd.CommandText = "SELECT CAST(value AS NVARCHAR(200)) FROM sys.extended_properties WHERE major_id = OBJECT_ID('dbo.vw_OwnershipTest') AND name = 'SchemaSmith_Product'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo(originalProduct));

        // Quench as different product — should fail because view is owned by originalProduct
        var ex = Assert.Catch<Exception>(() => RunIndexedViewQuench(cmd, wrongProduct, json));
        Assert.That(ex!.Message, Does.Contain("already"), "Should fail when view is owned by different product");

        conn.Close();
    }
}

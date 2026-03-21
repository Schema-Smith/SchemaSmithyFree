using System;
using System.Data;
using System.Threading;
using Schema.DataAccess;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class TableQuenchUpdateFillFactorTests : BaseTableQuenchTests
{
    private void RunTableQuenchWithUpdateFillFactor(IDbCommand cmd, string json, bool updateFillFactor)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = $"EXEC SchemaSmith.TableQuench @ProductName = '{_productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0, @UpdateFillFactor = {(updateFillFactor ? "1" : "0")}";
        var retry = true;
        var tries = 0;
        while (retry && tries++ < 10)
        {
            try
            {
                cmd.ExecuteNonQuery();
                retry = false;
            }
            catch (Exception e)
            {
                if (!e.Message.ContainsIgnoringCase("has been chosen as the deadlock victim")) throw;
                Thread.Sleep(1000);
            }
        }
    }

    private static int GetIndexFillFactor(IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $"SELECT ISNULL(NULLIF(fill_factor, 0), 100) FROM sys.indexes WITH (NOLOCK) WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = '{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Test]
    public void ShouldNotUpdateFillFactorWhenFlagIsFalseAtAllLevels()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table with an index at FillFactor 80
        var json = @"[{""Name"":""[UFTest_NoUpdate]"",""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_NoUpdate]"",""IndexColumns"":""Col1"",""FillFactor"":80}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, false);

        Assert.That(GetIndexFillFactor(cmd, "UFTest_NoUpdate", "IX_UFTest_NoUpdate"), Is.EqualTo(80));

        // Change FillFactor to 90 but don't enable UpdateFillFactor at any level
        json = @"[{""Name"":""[UFTest_NoUpdate]"",""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_NoUpdate]"",""IndexColumns"":""Col1"",""FillFactor"":90}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, false);

        // FillFactor should NOT change — still 80
        Assert.That(GetIndexFillFactor(cmd, "UFTest_NoUpdate", "IX_UFTest_NoUpdate"), Is.EqualTo(80));

        conn.Close();
    }

    [Test]
    public void ShouldUpdateFillFactorWhenProcLevelFlagIsTrue()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table with an index at FillFactor 80
        var json = @"[{""Name"":""[UFTest_ProcLevel]"",""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_ProcLevel]"",""IndexColumns"":""Col1"",""FillFactor"":80}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, false);

        Assert.That(GetIndexFillFactor(cmd, "UFTest_ProcLevel", "IX_UFTest_ProcLevel"), Is.EqualTo(80));

        // Change FillFactor to 90 with proc-level UpdateFillFactor=true
        json = @"[{""Name"":""[UFTest_ProcLevel]"",""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_ProcLevel]"",""IndexColumns"":""Col1"",""FillFactor"":90}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, true);

        // FillFactor SHOULD change to 90
        Assert.That(GetIndexFillFactor(cmd, "UFTest_ProcLevel", "IX_UFTest_ProcLevel"), Is.EqualTo(90));

        conn.Close();
    }

    [Test]
    public void ShouldUpdateFillFactorWhenIndexLevelFlagIsTrue()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table with an index at FillFactor 80 (no UpdateFillFactor at any level)
        var json = @"[{""Name"":""[UFTest_IndexLevel]"",""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_IndexLevel]"",""IndexColumns"":""Col1"",""FillFactor"":80}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, false);

        Assert.That(GetIndexFillFactor(cmd, "UFTest_IndexLevel", "IX_UFTest_IndexLevel"), Is.EqualTo(80));

        // Change FillFactor to 90 with per-index UpdateFillFactor=true (proc-level false)
        json = @"[{""Name"":""[UFTest_IndexLevel]"",""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_IndexLevel]"",""IndexColumns"":""Col1"",""FillFactor"":90,""UpdateFillFactor"":true}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, false);

        // FillFactor SHOULD change to 90 (per-index flag overrides)
        Assert.That(GetIndexFillFactor(cmd, "UFTest_IndexLevel", "IX_UFTest_IndexLevel"), Is.EqualTo(90));

        conn.Close();
    }

    [Test]
    public void ShouldUpdateFillFactorWhenTableLevelFlagIsTrue()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table with an index at FillFactor 80
        var json = @"[{""Name"":""[UFTest_TableLevel]"",""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_TableLevel]"",""IndexColumns"":""Col1"",""FillFactor"":80}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, false);

        Assert.That(GetIndexFillFactor(cmd, "UFTest_TableLevel", "IX_UFTest_TableLevel"), Is.EqualTo(80));

        // Change FillFactor to 90 with per-table UpdateFillFactor=true (proc-level false)
        json = @"[{""Name"":""[UFTest_TableLevel]"",""UpdateFillFactor"":true,""Columns"":[{""Name"":""Id"",""DataType"":""INT"",""Nullable"":false},{""Name"":""Col1"",""DataType"":""NVARCHAR(50)"",""Nullable"":true}],""Indexes"":[{""Name"":""[IX_UFTest_TableLevel]"",""IndexColumns"":""Col1"",""FillFactor"":90}]}]";
        RunTableQuenchWithUpdateFillFactor(cmd, json, false);

        // FillFactor SHOULD change to 90 (per-table flag propagates to all indexes)
        Assert.That(GetIndexFillFactor(cmd, "UFTest_TableLevel", "IX_UFTest_TableLevel"), Is.EqualTo(90));

        conn.Close();
    }
}

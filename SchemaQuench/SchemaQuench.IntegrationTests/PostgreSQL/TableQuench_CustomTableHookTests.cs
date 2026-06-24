// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

// Parity coverage for the recyclebin hooks on PostgreSQL: ModifiedTableQuench routes a removed table
// through a user-installed SchemaSmith.CustomTableDrop procedure when present, and
// MissingTableAndColumnQuench calls SchemaSmith.CustomTableRestore for a table being added and then
// does not recreate it if the restore brought it back. The hooks are detected per database, so each
// test runs against its own throwaway database (created, kindled, dropped here).
[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_CustomTableHookTests : BaseTableQuenchTests
{
    private const string Product = "Custom Hook Tests";

    [Test]
    public void ModifiedTableQuench_RoutesRemovedTableThroughCustomTableDropHook()
    {
        var db = UniqueDb("hookdrop");
        CreateDb(db);
        try
        {
            using var conn = (NpgsqlConnection)DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(DbConnString(db));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;
            ForgeKindler.KindleTheForge(cmd, Platform.PostgreSQL);

            Exec(cmd, "CREATE TABLE public.\"HookLog\" (\"Action\" TEXT, \"ObjName\" TEXT)");
            Exec(cmd, @"
CREATE OR REPLACE PROCEDURE ""SchemaSmith"".""CustomTableDrop""(p_Schema TEXT, p_Table TEXT) LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO public.""HookLog""(""Action"", ""ObjName"") VALUES ('DROP', p_Table);
  EXECUTE 'DROP TABLE ""' || p_Schema || '"".""' || p_Table || '""';
END $$");

            Quench(cmd, "[{\"Schema\":\"public\",\"Name\":\"Gadget\",\"Columns\":[{\"Name\":\"Id\",\"DataType\":\"integer\",\"Nullable\":false}],\"Indexes\":[{\"Name\":\"PK_Gadget\",\"PrimaryKey\":true,\"Unique\":true,\"IndexColumns\":\"Id\"}]}]", false);
            Quench(cmd, "[{\"Schema\":\"public\",\"Name\":\"Keeper\",\"Columns\":[{\"Name\":\"Id\",\"DataType\":\"integer\",\"Nullable\":false}],\"Indexes\":[{\"Name\":\"PK_Keeper\",\"PrimaryKey\":true,\"Unique\":true,\"IndexColumns\":\"Id\"}]}]", true);

            Assert.Multiple(() =>
            {
                Assert.That(ScalarLong(cmd, "SELECT COUNT(*) FROM public.\"HookLog\" WHERE \"Action\"='DROP' AND \"ObjName\"='Gadget'"), Is.EqualTo(1),
                    "CustomTableDrop hook should have been invoked for the removed table.");
                Assert.That(ScalarBool(cmd, "SELECT to_regclass('public.\"Gadget\"') IS NULL"), Is.True,
                    "Removed table should be gone (hook dropped it).");
            });
            conn.Close();
        }
        finally
        {
            DropDb(db);
        }
    }

    [Test]
    public void MissingTableAndColumnQuench_RestoresThroughCustomTableRestoreHookAndDoesNotRecreate()
    {
        var db = UniqueDb("hookrestore");
        CreateDb(db);
        try
        {
            using var conn = (NpgsqlConnection)DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(DbConnString(db));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;
            ForgeKindler.KindleTheForge(cmd, Platform.PostgreSQL);

            Exec(cmd, "CREATE TABLE public.\"HookLog\" (\"Action\" TEXT, \"ObjName\" TEXT)");
            Exec(cmd, @"
CREATE OR REPLACE PROCEDURE ""SchemaSmith"".""CustomTableRestore""(p_Schema TEXT, p_Table TEXT) LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO public.""HookLog""(""Action"", ""ObjName"") VALUES ('RESTORE', p_Table);
  IF p_Table = 'Widget' THEN
    EXECUTE 'CREATE TABLE ""' || p_Schema || '"".""Widget"" (""Id"" integer NOT NULL, CONSTRAINT ""PK_Widget"" PRIMARY KEY (""Id""))';
    EXECUTE 'INSERT INTO ""' || p_Schema || '"".""Widget"" (""Id"") VALUES (42)';
  END IF;
END $$");

            Quench(cmd, "[{\"Schema\":\"public\",\"Name\":\"Widget\",\"Columns\":[{\"Name\":\"Id\",\"DataType\":\"integer\",\"Nullable\":false}],\"Indexes\":[{\"Name\":\"PK_Widget\",\"PrimaryKey\":true,\"Unique\":true,\"IndexColumns\":\"Id\"}]}]", false);

            Assert.Multiple(() =>
            {
                Assert.That(ScalarLong(cmd, "SELECT COUNT(*) FROM public.\"HookLog\" WHERE \"Action\"='RESTORE' AND \"ObjName\"='Widget'"), Is.EqualTo(1),
                    "CustomTableRestore hook should have been invoked for the added table.");
                Assert.That(ScalarBool(cmd, "SELECT to_regclass('public.\"Widget\"') IS NOT NULL"), Is.True,
                    "Restored table should exist.");
                Assert.That(ScalarLong(cmd, "SELECT COUNT(*) FROM public.\"Widget\" WHERE \"Id\" = 42"), Is.EqualTo(1),
                    "Restored sentinel row must survive — the engine must not recreate the table empty.");
            });
            conn.Close();
        }
        finally
        {
            DropDb(db);
        }
    }

    private void Quench(IDbCommand cmd, string json, bool autoDrop)
    {
        cmd.CommandText = $"CALL \"SchemaSmith\".\"TableQuench\"(p_ProductName := '{Product}', p_TableDefinitions := '{json.Replace("'", "''")}', p_WhatIf := false, p_DropTablesRemovedFromProduct := {(autoDrop ? "true" : "false")}, p_DropUnknownIndexes := false)";
        cmd.ExecuteNonQuery();
    }

    private static void Exec(IDbCommand cmd, string sql)
    {
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static long ScalarLong(IDbCommand cmd, string sql)
    {
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static bool ScalarBool(IDbCommand cmd, string sql)
    {
        cmd.CommandText = sql;
        return (bool)cmd.ExecuteScalar()!;
    }

    private void CreateDb(string db)
    {
        using var conn = (NpgsqlConnection)DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{db}\"";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private void DropDb(string db)
    {
        try
        {
            using var conn = (NpgsqlConnection)DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{db}\" WITH (FORCE)";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
        catch { /* best-effort cleanup */ }
    }

    private static string DbConnString(string db)
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        return ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], db, config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
    }

    private static string UniqueDb(string prefix) => $"sshook_{prefix}_{Guid.NewGuid():N}".Substring(0, 40);
}

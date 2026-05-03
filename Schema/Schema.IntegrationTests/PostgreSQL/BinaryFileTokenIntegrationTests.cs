// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// End-to-end proof that <*BinaryFile*> resolution produces PostgreSQL BYTEA literal syntax
/// that a live PG engine actually accepts. Writes a real binary file to disk, resolves the
/// token via TokenHelper with Platform.PostgreSQL, substitutes the resolved value into an
/// INSERT statement, runs it against a real BYTEA column, and verifies the round-tripped
/// bytes match the original file content exactly.
/// </summary>
[Category("PostgreSQL")]
[TestFixture]
[Category("Integration")]
public class BinaryFileTokenIntegrationTests
{
    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void BinaryFileToken_RoundTripsThroughPostgresByteaColumn()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"schemasmith-bytea-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var fileRelativePath = "payload.bin";
        var filePath = Path.Combine(tempDir, fileRelativePath);

        // Representative byte spread: null byte, low ASCII, high ASCII, 0xFF — forces correct hex encoding
        // across the full 0x00-0xFF range rather than a lucky middle-of-range sample.
        var originalBytes = new byte[] { 0x00, 0x0C, 0xFF, 0x7F, 0x80, 0x01, 0x89, 0x50, 0x4E, 0x47 };
        File.WriteAllBytes(filePath, originalBytes);

        var tableName = $"_test_bytea_{Guid.NewGuid():N}"[..40];
        using var command = _connection.CreateCommand();

        try
        {
            command.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""payload"" BYTEA NOT NULL
)";
            command.ExecuteNonQuery();

            var tokens = new Dictionary<string, string>
            {
                { "MyBinary", $"{TokenHelper.BinaryFileTag}{fileRelativePath}" }
            };

            TokenHelper.ResolveFileTokens(tokens, tempDir, Platform.PostgreSQL);

            var resolved = tokens["MyBinary"];
            // PG E-string: the `\\` in the literal is parsed by PG as a single backslash,
            // giving it the `\x<hex>` form it needs for BYTEA input.
            Assert.That(resolved, Does.StartWith(@"E'\\x"));
            Assert.That(resolved, Does.EndWith("'::bytea"));

            command.CommandText = $@"INSERT INTO ""public"".""{tableName}"" (""id"", ""payload"") VALUES (1, {resolved})";
            command.ExecuteNonQuery();

            command.CommandText = $@"SELECT ""payload"" FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            var roundTripped = (byte[])command.ExecuteScalar()!;

            Assert.That(roundTripped, Is.EqualTo(originalBytes),
                "Bytes read back from BYTEA column must match the original file content exactly.");
        }
        finally
        {
            command.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            command.ExecuteNonQuery();

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

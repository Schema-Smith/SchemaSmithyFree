// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class ResolvedSqlArtifactWriterTests
{
    [Test]
    public void BuildArtifact_IncludesHeader_AndAllBatches_FailingBatchMarked()
    {
        var text = ResolvedSqlArtifactWriter.BuildArtifact(
            header: "Failed: srv.db [script 001.sql] — division by zero",
            batches: new List<string> { "CREATE TABLE A(...);", "SELECT 1/0;" },
            failingBatchIndex: 1);

        Assert.That(text, Does.Contain("Failed: srv.db"));
        Assert.That(text, Does.Contain("CREATE TABLE A"));
        Assert.That(text, Does.Contain("SELECT 1/0"));
        Assert.That(text, Does.Contain("FAILING BATCH"));
    }

    [Test]
    public void BuildArtifact_HeaderWithEmbeddedNewlines_StaysFullyCommented()
    {
        var text = ResolvedSqlArtifactWriter.BuildArtifact(
            header: "Failed: x — ERROR: relation does not exist\n\nPOSITION: 87",
            batches: new List<string> { "SELECT 1;" },
            failingBatchIndex: 0);

        // Every line of the header block must be a SQL comment — no bare line may escape.
        // Specifically, "POSITION: 87" must NOT appear on a line that doesn't start with --.
        foreach (var line in text.Replace("\r", "").Split('\n'))
            Assert.That(line == "" || line.StartsWith("--") || !line.Contains("POSITION"),
                $"Un-commented header fragment leaked into artifact body: '{line}'");

        // And stripping comment lines + GO must NOT leave any POSITION fragment in the executable SQL.
        var executable = string.Join("\n",
            text.Replace("\r", "").Split('\n')
                .Where(l => !l.StartsWith("--") && l.Trim() != "GO"));
        Assert.That(executable, Does.Not.Contain("POSITION"));
        Assert.That(executable, Does.Contain("SELECT 1"));
    }

    [Test]
    public void Scrub_RedactsSensitiveTokenValues_AboveMinLength()
    {
        var sql = "CREATE LOGIN x WITH PASSWORD = 'hunter2secret';";
        var sensitive = new List<KeyValuePair<string, string>> { new("AdminPassword", "hunter2secret") };
        var scrubbed = ResolvedSqlArtifactWriter.Scrub(sql, sensitive);

        Assert.That(scrubbed, Does.Not.Contain("hunter2secret"));
        Assert.That(scrubbed, Does.Contain(LogScrubber.Mask));
    }

    [Test]
    public void Scrub_DoesNotRedact_ShortValues_AvoidingOverRedaction()
    {
        var sql = "UPDATE t SET col = 'a' WHERE id = 1;";
        var sensitive = new List<KeyValuePair<string, string>> { new("Secret", "a") };
        var scrubbed = ResolvedSqlArtifactWriter.Scrub(sql, sensitive);

        Assert.That(scrubbed, Is.EqualTo(sql)); // unchanged — below min length guard
    }

    [Test]
    public void Scrub_AppliesConnectionStringCatchAll()
    {
        var sql = "-- conn: Server=x;Password=plaintextpw;";
        var scrubbed = ResolvedSqlArtifactWriter.Scrub(sql, new List<KeyValuePair<string, string>>());
        Assert.That(scrubbed, Does.Not.Contain("plaintextpw"));
    }

    [Test]
    public void WriteFailureArtifact_ScrubTrue_RedactsSensitiveValue_AndReturnsWrittenPath()
    {
        var tempDir = Directory.CreateTempSubdirectory("ss_artifact_").FullName;
        var fileName = "artifact.sql";

        try
        {
            var sensitive = new List<KeyValuePair<string, string>> { new("AdminPassword", "hunter2secret") };
            var returnedPath = ResolvedSqlArtifactWriter.WriteFailureArtifact(
                directory: tempDir,
                scrub: true,
                sensitiveValues: sensitive,
                header: "Failed: srv.db [script 001.sql] — division by zero",
                batches: new List<string> { "CREATE LOGIN x WITH PASSWORD = 'hunter2secret';" },
                failingBatchIndex: 0,
                fileName: fileName);

            Assert.That(Path.GetDirectoryName(returnedPath), Is.EqualTo(tempDir));
            Assert.That(Path.GetFileName(returnedPath), Is.EqualTo(fileName));
            var written = File.ReadAllText(returnedPath);
            Assert.That(written, Does.Not.Contain("hunter2secret"));
            Assert.That(written, Does.Contain(LogScrubber.Mask));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void WriteFailureArtifact_ScrubFalse_LeavesSensitiveValueIntact()
    {
        var tempDir = Directory.CreateTempSubdirectory("ss_artifact_").FullName;
        var fileName = "artifact.sql";

        try
        {
            var sensitive = new List<KeyValuePair<string, string>> { new("AdminPassword", "hunter2secret") };
            var returnedPath = ResolvedSqlArtifactWriter.WriteFailureArtifact(
                directory: tempDir,
                scrub: false,
                sensitiveValues: sensitive,
                header: "Failed: srv.db [script 001.sql] — division by zero",
                batches: new List<string> { "CREATE LOGIN x WITH PASSWORD = 'hunter2secret';" },
                failingBatchIndex: 0,
                fileName: fileName);

            Assert.That(Path.GetDirectoryName(returnedPath), Is.EqualTo(tempDir));
            Assert.That(Path.GetFileName(returnedPath), Is.EqualTo(fileName));
            var written = File.ReadAllText(returnedPath);
            Assert.That(written, Does.Contain("hunter2secret"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void WriteFailureArtifact_LabelSanitizingOverload_SanitizesLabelIntoFileName_AndWritesContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rsaw_{Guid.NewGuid():N}");
        try
        {
            var path = ResolvedSqlArtifactWriter.WriteFailureArtifact(
                dir, scrub: false, sensitiveValues: new List<KeyValuePair<string, string>>(),
                header: "Failed data delivery: srv.db [dbo.Orders#EU/1]",
                batches: new List<string> { "SELECT 1" }, failingBatchIndex: 0,
                label: "dbo.Orders#EU/1",
                fileNameFromSafeLabel: safe => $"Failed DataDelivery {safe}.sql");

            Assert.That(Path.GetFileName(path), Does.Not.Contain("/"),
                "The '/' in the label must be sanitized out of the filename.");
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path), Does.Contain("SELECT 1"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Test]
    public void Write_CreatesDirectory_WhenMissing()
    {
        // Use a fresh GUID subdir under temp — guaranteed not to exist before this call.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fileName = "artifact.sql";
        var content = "-- test artifact";

        try
        {
            Assert.That(Directory.Exists(tempDir), Is.False, "Precondition: directory must not exist before Write");

            var returnedPath = ResolvedSqlArtifactWriter.Write(tempDir, fileName, content);

            Assert.That(File.Exists(returnedPath), Is.True, "Write must create the file in the missing directory");
            Assert.That(returnedPath, Is.EqualTo(Path.Combine(tempDir, fileName)));
            Assert.That(File.ReadAllText(returnedPath), Is.EqualTo(content));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

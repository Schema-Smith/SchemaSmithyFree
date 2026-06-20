// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
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
}

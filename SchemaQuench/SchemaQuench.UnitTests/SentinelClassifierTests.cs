// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Npgsql;
using NUnit.Framework;
using SchemaQuench;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class SentinelClassifierTests
{
    [Test]
    public void Recognizes_ExactConstant()
        => Assert.That(SentinelClassifier.IsShouldNotApply(new Exception("SCHEMASMITH: SHOULD NOT APPLY")), Is.True);

    [Test]
    public void Recognizes_CaseInsensitive_AndTrimmed()
        => Assert.That(SentinelClassifier.IsShouldNotApply(new Exception("  schemasmith: should not apply  ")), Is.True);

    [Test]
    public void Recognizes_Wrapped_SqlServerErrorException()
        => Assert.That(SentinelClassifier.IsShouldNotApply(
            new SqlServerErrorException(50000, "SCHEMASMITH: SHOULD NOT APPLY")), Is.True);

    [Test]
    public void Recognizes_AcrossInnerExceptionChain()
        => Assert.That(SentinelClassifier.IsShouldNotApply(
            new Exception("outer", new Exception("SCHEMASMITH: SHOULD NOT APPLY"))), Is.True);

    /// <summary>
    /// Npgsql 8+ sets <c>PostgresException.Message</c> to <c>"{SqlState}: {MessageText}"</c>
    /// (e.g., <c>"P0001: SCHEMASMITH: SHOULD NOT APPLY"</c>). The classifier must recognise the
    /// sentinel via <c>MessageText</c> rather than falling back to the prefixed <c>Message</c>.
    /// </summary>
    [Test]
    public void Recognizes_PostgresException_ViaMsgText_WhenMessageHasSqlStatePrefix()
    {
        // PostgresException("messageText", severity, invariantSeverity, sqlState) — the first
        // arg IS MessageText. Message is auto-set to "{sqlState}: {MessageText}" by Npgsql 8+.
        var ex = new PostgresException("SCHEMASMITH: SHOULD NOT APPLY", "ERROR", "ERROR", "P0001");
        Assert.That(SentinelClassifier.IsShouldNotApply(ex), Is.True,
            "PostgresException with sentinel in MessageText must be recognised even though Message has the 'P0001: ' prefix.");
    }

    [Test]
    public void Rejects_PostgresException_WithUnrelatedMessage()
    {
        var ex = new PostgresException("relation \"foo\" does not exist", "ERROR", "ERROR", "42P01");
        Assert.That(SentinelClassifier.IsShouldNotApply(ex), Is.False);
    }

    [Test]
    public void Rejects_SubstringInLargerMessage()
        => Assert.That(SentinelClassifier.IsShouldNotApply(
            new Exception("ERROR: this object SCHEMASMITH: SHOULD NOT APPLY here, syntax error")), Is.False);

    [Test]
    public void Rejects_UnrelatedError()
        => Assert.That(SentinelClassifier.IsShouldNotApply(new Exception("Invalid column name 'Foo'")), Is.False);

    [Test]
    public void Rejects_Null()
        => Assert.That(SentinelClassifier.IsShouldNotApply(null), Is.False);
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
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

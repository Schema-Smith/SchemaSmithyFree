// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using NSubstitute;
using Schema.Delivery;

namespace Schema.UnitTests.Delivery;

[TestFixture]
public class GateEvaluatorTests
{
    [Test]
    public void ShouldApply_BlankExpression_ReturnsTrue_WithoutRunningQuery()
    {
        var cmd = Substitute.For<IDbCommand>();
        Assert.That(GateEvaluator.ShouldApply(cmd, "   "), Is.True);
        cmd.DidNotReceive().ExecuteScalar();
    }

    [Test]
    public void ShouldApply_TruthyScalar_ReturnsTrue()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns(1);
        Assert.That(GateEvaluator.ShouldApply(cmd, "DB_NAME() = 'Dev'"), Is.True);
    }

    [Test]
    public void ShouldApply_FalsyScalar_ReturnsFalse()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns(0);
        Assert.That(GateEvaluator.ShouldApply(cmd, "DB_NAME() = 'Prod'"), Is.False);
    }

    [Test]
    public void NormalizeToSelect_BarePredicate_WrappedInSelectCase()
    {
        Assert.That(GateEvaluator.NormalizeToSelect("1 = 1"),
            Is.EqualTo("SELECT CASE WHEN (1 = 1) THEN 1 ELSE 0 END"));
    }

    [Test]
    public void NormalizeToSelect_AlreadySelect_ReturnedAsIs()
    {
        Assert.That(GateEvaluator.NormalizeToSelect("SELECT 1"), Is.EqualTo("SELECT 1"));
    }

    [TestCase(null, false)]
    [TestCase(1, true)]
    [TestCase(0, false)]
    [TestCase(true, true)]
    public void ScalarToBool_CoercesAsExpected(object input, bool expected)
    {
        Assert.That(GateEvaluator.ScalarToBool(input), Is.EqualTo(expected));
    }
}

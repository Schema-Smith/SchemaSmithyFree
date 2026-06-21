// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NSubstitute;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class FolderGateTests
{
    private static IDbCommand CommandReturning(object scalar)
    {
        var command = Substitute.For<IDbCommand>();
        command.ExecuteScalar().Returns(scalar);
        return command;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ShouldApply_BlankExpression_ReturnsTrueWithoutExecuting(string expression)
    {
        var command = Substitute.For<IDbCommand>();

        Assert.That(FolderGate.ShouldApply(command, expression), Is.True);
        command.DidNotReceive().ExecuteScalar();
    }

    [Test]
    public void ShouldApply_TruthyScalar_ReturnsTrue()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FolderGate.ShouldApply(CommandReturning(1), "SELECT 1"), Is.True);
            Assert.That(FolderGate.ShouldApply(CommandReturning(true), "SELECT true"), Is.True);
            Assert.That(FolderGate.ShouldApply(CommandReturning(1L), "SELECT 1"), Is.True);
        });
    }

    [Test]
    public void ShouldApply_FalsyScalar_ReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FolderGate.ShouldApply(CommandReturning(0), "SELECT 0"), Is.False);
            Assert.That(FolderGate.ShouldApply(CommandReturning(false), "SELECT false"), Is.False);
            Assert.That(FolderGate.ShouldApply(CommandReturning(null), "SELECT NULL"), Is.False);
        });
    }

    [Test]
    public void ShouldApply_SetsCommandTextToExpression()
    {
        var command = CommandReturning(1);

        FolderGate.ShouldApply(command, "SELECT CASE WHEN @@version LIKE '%MariaDB%' THEN 1 ELSE 0 END");

        command.Received().CommandText = "SELECT CASE WHEN @@version LIKE '%MariaDB%' THEN 1 ELSE 0 END";
    }

    [Test]
    public void ShouldApply_ExpressionThrows_PropagatesForFailClosedHandling()
    {
        var command = Substitute.For<IDbCommand>();
        command.ExecuteScalar().Returns(_ => throw new InvalidOperationException("syntax error"));

        Assert.Throws<InvalidOperationException>(() => FolderGate.ShouldApply(command, "NOT VALID SQL"));
    }
}

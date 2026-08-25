// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using NSubstitute;
using NUnit.Framework;

namespace SchemaTongs.UnitTests;

/// <summary>
/// The stubbing semantics themselves, because they are the thing that was wrong. NSubstitute's
/// <c>Returns(a, b)</c> hands back <c>b</c> for every call after the second, so a reader stubbed for one
/// query was silently inherited by whatever query ran next — which is why 23 tests here passed by accident
/// of call order rather than because the code under test was right.
/// </summary>
public class ReaderStubExtensionsTests
{
    [Test]
    public void AReaderIsHandedOutOnceAndNotInheritedByTheNextQuery()
    {
        var command = Substitute.For<IDbCommand>();
        var first = Substitute.For<IDataReader>();
        first.Read().Returns(true);

        command.StubReaders(first);

        Assert.That(command.ExecuteReader(), Is.SameAs(first), "the stubbed reader feeds the query it was for");

        var next = command.ExecuteReader();
        Assert.That(next, Is.Not.SameAs(first),
            "a second query must NOT receive the first query's reader — that is the inheritance that made "
            + "test outcomes depend on which cast happened to run first");
        Assert.That(next.Read(), Is.False, "an unstubbed query must see no rows");
    }

    [Test]
    public void ReadersAreHandedOutInOrder()
    {
        var command = Substitute.For<IDbCommand>();
        var a = Substitute.For<IDataReader>();
        var b = Substitute.For<IDataReader>();

        command.StubReaders(a, b);

        Assert.Multiple(() =>
        {
            Assert.That(command.ExecuteReader(), Is.SameAs(a));
            Assert.That(command.ExecuteReader(), Is.SameAs(b));
            Assert.That(command.ExecuteReader().Read(), Is.False, "and exhausted thereafter");
        });
    }

    [Test]
    public void AnExhaustedReaderAnswersItsIndexerRatherThanReturningNull()
    {
        // The original crash was a NullReferenceException from reader["SchemaName"].ToString() on an
        // inherited reader whose indexer nobody had stubbed. A null there names nothing; "" is at least
        // a value the caller can reason about.
        var reader = ReaderStubExtensions.Exhausted();
        Assert.That(reader["SchemaName"], Is.EqualTo(""));
        Assert.That(reader[0], Is.EqualTo(""));
    }
}

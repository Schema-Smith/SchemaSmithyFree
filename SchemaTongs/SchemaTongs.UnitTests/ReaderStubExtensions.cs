// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Data;
using NSubstitute;

namespace SchemaTongs.UnitTests;

/// <summary>
/// Stubs a command's readers so a test's readers feed only the queries that test is about.
/// <para>
/// <c>ExecuteReader().Returns(a, b)</c> hands back <c>a</c>, then <c>b</c>, then <b><c>b</c> again for
/// every call after that</b> — so the last reader is inherited by whatever query happens to run next.
/// That is why 23 tests here passed by accident of call order: their target method ran before
/// <c>CastSqlServerIndexedViews</c>, which reads by string indexer rather than by ordinal, and an
/// unstubbed indexer returns null so <c>.ToString()</c> throws. Add a test whose target lands later in
/// the order and it fails for reasons that have nothing to do with what it is testing.
/// </para>
/// <para>
/// This hands back each reader once and then an exhausted one forever. A cast the test never intended to
/// exercise sees no rows instead of re-consuming somebody else's.
/// </para>
/// </summary>
internal static class ReaderStubExtensions
{
    internal static void StubReaders(this IDbCommand command, params IDataReader[] readers)
    {
        var queue = new Queue<IDataReader>(readers);
        command.ExecuteReader().Returns(_ => queue.Count > 0 ? queue.Dequeue() : Exhausted());
    }

    /// <summary>A reader with no rows, whose indexer answers rather than returning null.</summary>
    internal static IDataReader Exhausted()
    {
        var reader = Substitute.For<IDataReader>();
        reader.Read().Returns(false);
        // Answer the indexer too. A cast that reads a column before checking Read() would otherwise get
        // null and throw a NullReferenceException naming nothing -- the original symptom.
        reader[Arg.Any<string>()].Returns("");
        reader[Arg.Any<int>()].Returns("");
        return reader;
    }
}

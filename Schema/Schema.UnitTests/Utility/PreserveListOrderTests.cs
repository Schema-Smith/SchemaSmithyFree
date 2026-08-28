// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

/// <summary>
/// A re-extract should diff to what actually changed. Sorting the whole file every time buries a
/// one-column change under a whole-file reshuffle, and destroys the ordering of a hand-authored package
/// where the sequence usually carries meaning.
/// </summary>
public class PreserveListOrderTests
{
    private static Table TableWith(params string[] columnNames) =>
        new() { Name = "[T]", Columns = columnNames.Select(n => new Column { Name = n }).ToList() };

    private static IEnumerable<string> Names(Table t) => t.Columns.Select(c => c.Name);

    [Test]
    public void ExistingEntriesKeepTheOrderTheFileHad()
    {
        var original = TableWith("[Zebra]", "[Apple]", "[Mango]");   // hand-authored, deliberately unsorted
        var extracted = TableWith("[Apple]", "[Mango]", "[Zebra]");  // extraction sorted them

        ImportTableHelper.PreserveListOrder(extracted, original, ObjectOrder.Name);

        Assert.That(Names(extracted), Is.EqualTo(new[] { "[Zebra]", "[Apple]", "[Mango]" }),
            "the author's sequence must survive a re-extract");
    }

    [Test]
    public void NewEntriesAppendInTheFallbackOrder()
    {
        var original = TableWith("[Zebra]", "[Apple]");
        var extracted = TableWith("[Apple]", "[Beta]", "[Alpha]", "[Zebra]");

        ImportTableHelper.PreserveListOrder(extracted, original, ObjectOrder.Name);

        Assert.That(Names(extracted), Is.EqualTo(new[] { "[Zebra]", "[Apple]", "[Alpha]", "[Beta]" }),
            "known entries hold their place; genuinely new ones append, sorted by name");
    }

    [Test]
    public void PhysicalFallbackLeavesNewEntriesAsExtractionProducedThem()
    {
        var original = TableWith("[Zebra]");
        var extracted = TableWith("[Zebra]", "[Beta]", "[Alpha]");

        ImportTableHelper.PreserveListOrder(extracted, original, ObjectOrder.Physical);

        Assert.That(Names(extracted), Is.EqualTo(new[] { "[Zebra]", "[Beta]", "[Alpha]" }),
            "Physical means 'as the table has them', so new entries are not re-sorted");
    }

    [Test]
    public void EntriesRemovedFromTheDatabaseDropOut()
    {
        var original = TableWith("[Zebra]", "[Gone]", "[Apple]");
        var extracted = TableWith("[Apple]", "[Zebra]");

        ImportTableHelper.PreserveListOrder(extracted, original, ObjectOrder.Name);

        Assert.That(Names(extracted), Is.EqualTo(new[] { "[Zebra]", "[Apple]" }),
            "preserving order must not resurrect a column the database no longer has");
    }

    [Test]
    public void QuotingDifferencesStillMatch()
    {
        // A hand-authored file may quote differently from extraction. Treating those as all-new would
        // append everything and reshuffle the whole file -- the exact churn this exists to prevent.
        var original = new Table
        {
            Name = "[T]",
            Columns = [new Column { Name = "Zebra" }, new Column { Name = "\"Apple\"" }]
        };
        var extracted = TableWith("[Apple]", "[Zebra]");

        ImportTableHelper.PreserveListOrder(extracted, original, ObjectOrder.Name);

        Assert.That(Names(extracted), Is.EqualTo(new[] { "[Zebra]", "[Apple]" }));
    }

    [Test]
    public void AFirstExtractIsUntouched()
    {
        var extracted = TableWith("[Apple]", "[Zebra]");
        Assert.DoesNotThrow(() => ImportTableHelper.PreserveListOrder(extracted, null, ObjectOrder.Name));
        Assert.That(Names(extracted), Is.EqualTo(new[] { "[Apple]", "[Zebra]" }),
            "there is nothing to preserve against, so extraction's own order stands");
    }
}

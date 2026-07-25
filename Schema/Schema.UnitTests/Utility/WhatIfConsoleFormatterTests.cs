// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class WhatIfConsoleFormatterTests
{
    [TestCase("concise", WhatIfDetail.Concise)]
    [TestCase("CONCISE", WhatIfDetail.Concise)]
    [TestCase("verbose", WhatIfDetail.Verbose)]
    [TestCase("normal", WhatIfDetail.Normal)]
    [TestCase("", WhatIfDetail.Normal)]
    [TestCase(null, WhatIfDetail.Normal)]
    [TestCase("nonsense", WhatIfDetail.Normal)]
    public void ParseDetail_MapsKnownValues_DefaultsToNormal(string value, WhatIfDetail expected)
    {
        Assert.That(WhatIfConsoleFormatter.ParseDetail(value), Is.EqualTo(expected));
    }

    [Test]
    public void Render_Normal_EmitsOneLinePerEntry_UnchangedFormat()
    {
        var entries = new List<WhatIfConsoleEntry>
        {
            new("apply", "APPLY", "a.sql"),
            new("skip", "SKIP (previously quenched)", "b.sql")
        };

        var lines = WhatIfConsoleFormatter.Render(entries, WhatIfDetail.Normal).ToList();

        Assert.That(lines, Is.EqualTo(new[]
        {
            "    Would APPLY: a.sql",
            "    Would SKIP (previously quenched): b.sql"
        }));
    }

    [Test]
    public void Render_Verbose_MatchesNormal()
    {
        var entries = new List<WhatIfConsoleEntry> { new("deliver", "DELIVER", "x") };

        var verbose = WhatIfConsoleFormatter.Render(entries, WhatIfDetail.Verbose).ToList();
        var normal = WhatIfConsoleFormatter.Render(entries, WhatIfDetail.Normal).ToList();

        Assert.That(verbose, Is.EqualTo(normal));
    }

    [Test]
    public void Render_Concise_CollapsesToPerCategoryCounts()
    {
        var entries = new List<WhatIfConsoleEntry>
        {
            new("apply", "APPLY", "a.sql"),
            new("apply", "APPLY", "b.sql"),
            new("skip", "SKIP (previously quenched)", "c.sql")
        };

        var lines = WhatIfConsoleFormatter.Render(entries, WhatIfDetail.Concise).ToList();

        Assert.That(lines, Has.Count.EqualTo(1));
        Assert.That(lines[0], Does.Contain("2 would apply").And.Contain("1 would skip"));
    }

    [Test]
    public void Render_Concise_NoEntries_EmitsNothing()
    {
        var lines = WhatIfConsoleFormatter.Render(new List<WhatIfConsoleEntry>(), WhatIfDetail.Concise).ToList();

        Assert.That(lines, Is.Empty);
    }
}

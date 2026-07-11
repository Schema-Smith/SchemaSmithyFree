// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class VariantAttributionTests
{
    // Variants are represented by their gate expression string for these pure tests;
    // gateOf is identity and isActive is a supplied predicate.
    private static VariantDecision Decide(IReadOnlyList<string> gates, Func<string, bool> isActive)
        => VariantAttribution.Decide(gates, g => g, isActive);

    [Test]
    public void Decide_ExactlyOneActive_RefreshesThatVariant()
    {
        var d = Decide(["a", "b", "c"], g => g == "b");
        Assert.That(d.Action, Is.EqualTo(VariantAction.RefreshActive));
        Assert.That(d.ActiveIndex, Is.EqualTo(1));
    }

    [Test]
    public void Decide_NoneActive_EmitsUngated()
    {
        var d = Decide(["a", "b"], _ => false);
        Assert.That(d.Action, Is.EqualTo(VariantAction.EmitUngated));
        Assert.That(d.ActiveIndex, Is.EqualTo(-1));
    }

    [Test]
    public void Decide_MultipleActive_EmitsUngated()
    {
        var d = Decide(["a", "b"], _ => true);
        Assert.That(d.Action, Is.EqualTo(VariantAction.EmitUngated));
    }

    [Test]
    public void Decide_GateEvaluationThrows_Propagates_FailClosed()
    {
        Assert.That(() => Decide(["a", "b"], _ => throw new InvalidOperationException("bad gate")),
            Throws.InvalidOperationException);
    }
}

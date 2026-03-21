using NSubstitute;
using Schema.Utility;
using System.Data;
using System.Linq;

namespace Schema.UnitTests;

public class ForgeKindlerTests
{
    [Test]
    public void ShouldReplaceParseJsonTokenInTableQuench()
    {
        var command = Substitute.For<IDbCommand>();
        var executedScripts = new System.Collections.Generic.List<string>();
        command.When(c => c.ExecuteNonQuery()).Do(_ => executedScripts.Add(command.CommandText));

        ForgeKindler.KindleTheForge(command);

        var tableQuenchScript = executedScripts.FirstOrDefault(s => s.Contains("SchemaSmith.TableQuench"));
        Assert.That(tableQuenchScript, Is.Not.Null, "TableQuench script should be deployed");
        Assert.That(tableQuenchScript, Does.Not.Contain("{{ParseJson}}"), "ParseJson token should be replaced");
        Assert.That(tableQuenchScript, Does.Contain("Parse Tables from Json"), "Should contain injected ParseJson content");
    }

    [Test]
    public void ShouldDeployPrintWithNoWaitBeforeModularProcs()
    {
        var command = Substitute.For<IDbCommand>();
        var executedScripts = new System.Collections.Generic.List<string>();
        command.When(c => c.ExecuteNonQuery()).Do(_ => executedScripts.Add(command.CommandText));

        ForgeKindler.KindleTheForge(command);

        var printIndex = executedScripts.FindIndex(s => s.Contains("PrintWithNoWait"));
        var missingTableIndex = executedScripts.FindIndex(s => s.Contains("MissingTableAndColumnQuench"));
        Assert.That(printIndex, Is.GreaterThan(-1), "PrintWithNoWait should be deployed");
        Assert.That(missingTableIndex, Is.GreaterThan(-1), "MissingTableAndColumnQuench should be deployed");
        Assert.That(printIndex, Is.LessThan(missingTableIndex), "PrintWithNoWait must be deployed before modular procs");
    }

    [Test]
    public void ShouldDeployModularProcsBeforeTableQuenchWrapper()
    {
        var command = Substitute.For<IDbCommand>();
        var executedScripts = new System.Collections.Generic.List<string>();
        command.When(c => c.ExecuteNonQuery()).Do(_ => executedScripts.Add(command.CommandText));

        ForgeKindler.KindleTheForge(command);

        var foreignKeyIndex = executedScripts.FindIndex(s => s.Contains("ForeignKeyQuench"));
        var tableQuenchIndex = executedScripts.FindIndex(s => s.Contains("SchemaSmith.TableQuench") && s.Contains("MissingTableAndColumnQuench"));
        Assert.That(foreignKeyIndex, Is.GreaterThan(-1), "ForeignKeyQuench should be deployed");
        Assert.That(tableQuenchIndex, Is.GreaterThan(-1), "TableQuench wrapper should be deployed");
        Assert.That(foreignKeyIndex, Is.LessThan(tableQuenchIndex), "Modular procs must be deployed before wrapper");
    }
}

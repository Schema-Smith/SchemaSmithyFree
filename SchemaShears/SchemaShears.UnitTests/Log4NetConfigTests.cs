// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using System.Linq;
using NUnit.Framework;

namespace SchemaShears.UnitTests;

// Regression guard: the log4net config was copy/pasted from DataTongs and named SchemaShears's
// logs "DataTongs - *.log", so LogBackup (which looks for "<appName> - *.log") never captured them.
[TestFixture]
public class Log4NetConfigTests
{
    [Test]
    public void EmbeddedLog4NetConfig_NamesLogsForSchemaShears()
    {
        var asm = typeof(Program).Assembly;
        var resourceName = asm.GetManifestResourceNames().Single(n => n.EndsWith("Log4Net.config"));
        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        Assert.That(content, Does.Contain("SchemaShears - Progress.log"));
        Assert.That(content, Does.Contain("SchemaShears - Errors.log"));
        Assert.That(content, Does.Not.Contain("DataTongs"),
            "log4net config must not reference another tool's name (copy/paste artifact)");
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class LogScrubberTests
{
    private static LogHygieneOptions Options(
        bool logTokens = true,
        IEnumerable<string> scrubTokens = null,
        IEnumerable<string> scrubPatterns = null,
        IEnumerable<string> allowTokens = null)
    {
        var options = new LogHygieneOptions { LogTokens = logTokens };
        if (scrubTokens != null) foreach (var t in scrubTokens) options.ScrubTokens.Add(t);
        if (scrubPatterns != null) options.ScrubPatterns.AddRange(scrubPatterns);
        if (allowTokens != null) foreach (var t in allowTokens) options.AllowTokens.Add(t);
        return options;
    }

    // ---- ShouldScrubName: default patterns ----

    [TestCase("DbPassword")]
    [TestCase("Password")]
    [TestCase("ConnectionPwd")]
    [TestCase("ClientSecret")]
    [TestCase("ApiKey")]
    [TestCase("AuthToken")]
    [TestCase("ConnectionString")]
    [TestCase("AwsCredential")]
    public void ShouldScrubName_MatchesDefaultSensitivePatterns(string name)
    {
        Assert.That(LogScrubber.ShouldScrubName(name, LogHygieneOptions.Default), Is.True);
    }

    [TestCase("PASSWORD")]
    [TestCase("dbpassword")]
    [TestCase("ApIkEy")]
    public void ShouldScrubName_IsCaseInsensitive(string name)
    {
        Assert.That(LogScrubber.ShouldScrubName(name, LogHygieneOptions.Default), Is.True);
    }

    [TestCase("ServerName")]
    [TestCase("MaxThreads")]
    [TestCase("MainDB")]
    [TestCase("")]
    public void ShouldScrubName_DoesNotMatchNonSensitiveNames(string name)
    {
        Assert.That(LogScrubber.ShouldScrubName(name, LogHygieneOptions.Default), Is.False);
    }

    // ---- ShouldScrubName: config knobs ----

    [Test]
    public void ShouldScrubName_ScrubTokens_OptInForUnmatchedName()
    {
        var options = Options(scrubTokens: ["Handshake"]);
        Assert.That(LogScrubber.ShouldScrubName("Handshake", options), Is.True);
    }

    [Test]
    public void ShouldScrubName_ScrubPatterns_OptInWithGlobStar()
    {
        var options = Options(scrubPatterns: ["*Salt*"]);
        Assert.That(LogScrubber.ShouldScrubName("SessionSalt", options), Is.True);
    }

    [Test]
    public void ShouldScrubName_BareGlobPattern_DoesNotScrubNonSensitiveNames()
    {
        // A bare "*" trims to an empty string; an unguarded contains-match would then match
        // every name. Suppress-all is LogTokens:false, not a wildcard pattern — a bare "*" is
        // a config mistake and must not silently scrub innocuous names.
        var options = Options(scrubPatterns: ["*"]);
        Assert.That(LogScrubber.ShouldScrubName("ServerName", options), Is.False);
    }

    [Test]
    public void ShouldScrubName_AllowTokens_OptOutOfDefaultPattern()
    {
        var options = Options(allowTokens: ["PublicToken"]);
        Assert.That(LogScrubber.ShouldScrubName("PublicToken", options), Is.False);
    }

    [Test]
    public void ShouldScrubName_AllowTokens_BeatsScrubTokens()
    {
        var options = Options(scrubTokens: ["Ambiguous"], allowTokens: ["Ambiguous"]);
        Assert.That(LogScrubber.ShouldScrubName("Ambiguous", options), Is.False);
    }

    [Test]
    public void ShouldScrubName_NullOptions_FallsBackToDefaults()
    {
        Assert.That(LogScrubber.ShouldScrubName("DbPassword", null), Is.True);
        Assert.That(LogScrubber.ShouldScrubName("ServerName", null), Is.False);
    }

    // ---- ScrubConnectionStringSubfields ----

    [Test]
    public void ScrubConnectionStringSubfields_StripsPassword()
    {
        var result = LogScrubber.ScrubConnectionStringSubfields("Server=db1;Password=secret;Database=app");
        Assert.That(result, Is.EqualTo("Server=db1;Password=***;Database=app"));
    }

    [Test]
    public void ScrubConnectionStringSubfields_StripsPwd()
    {
        var result = LogScrubber.ScrubConnectionStringSubfields("Server=db1;Pwd=hunter2;Database=app");
        Assert.That(result, Is.EqualTo("Server=db1;Pwd=***;Database=app"));
    }

    [Test]
    public void ScrubConnectionStringSubfields_IsCaseInsensitiveAndPreservesKeyCasing()
    {
        var result = LogScrubber.ScrubConnectionStringSubfields("Server=db1;PASSWORD=secret");
        Assert.That(result, Is.EqualTo("Server=db1;PASSWORD=***"));
    }

    [Test]
    public void ScrubConnectionStringSubfields_HandlesPasswordAtEnd()
    {
        var result = LogScrubber.ScrubConnectionStringSubfields("Server=db1;Password=secret");
        Assert.That(result, Is.EqualTo("Server=db1;Password=***"));
    }

    [Test]
    public void ScrubConnectionStringSubfields_StripsDoubleQuotedPasswordContainingSemicolon()
    {
        // ADO.NET allows quoting a value that itself contains ';'. A naive [^;]* stops at the
        // first inner ';' and leaks the tail of the password.
        var result = LogScrubber.ScrubConnectionStringSubfields("Server=db1;Password=\"p;q\";Database=app");
        Assert.That(result, Is.EqualTo("Server=db1;Password=***;Database=app"));
    }

    [Test]
    public void ScrubConnectionStringSubfields_StripsSingleQuotedPasswordContainingSemicolon()
    {
        var result = LogScrubber.ScrubConnectionStringSubfields("Server=db1;Password='p;q';Database=app");
        Assert.That(result, Is.EqualTo("Server=db1;Password=***;Database=app"));
    }

    [Test]
    public void ScrubConnectionStringSubfields_StripsBraceQuotedPwdContainingSemicolon()
    {
        var result = LogScrubber.ScrubConnectionStringSubfields("Server=db1;Pwd={a;b};Database=app");
        Assert.That(result, Is.EqualTo("Server=db1;Pwd=***;Database=app"));
    }

    [Test]
    public void ScrubConnectionStringSubfields_PreservesNonSensitiveFields()
    {
        const string value = "Server=db1;Database=app;User Id=admin";
        Assert.That(LogScrubber.ScrubConnectionStringSubfields(value), Is.EqualTo(value));
    }

    [Test]
    public void ScrubConnectionStringSubfields_DoesNotMatchPasswordSubstringInOtherKeys()
    {
        const string value = "Server=db1;MyPasswordHint=abc";
        Assert.That(LogScrubber.ScrubConnectionStringSubfields(value), Is.EqualTo(value));
    }

    [Test]
    public void ScrubConnectionStringSubfields_NullOrEmptyReturnedAsIs()
    {
        Assert.That(LogScrubber.ScrubConnectionStringSubfields(null), Is.Null);
        Assert.That(LogScrubber.ScrubConnectionStringSubfields(""), Is.EqualTo(""));
    }

    // ---- ScrubTokenValue ----

    [Test]
    public void ScrubTokenValue_SensitiveName_MasksWholeValue()
    {
        Assert.That(LogScrubber.ScrubTokenValue("DbPassword", "secret123", LogHygieneOptions.Default), Is.EqualTo("***"));
    }

    [Test]
    public void ScrubTokenValue_NonSensitiveName_ReturnsValueVerbatim()
    {
        Assert.That(LogScrubber.ScrubTokenValue("MainDB", "TestMain", LogHygieneOptions.Default), Is.EqualTo("TestMain"));
    }

    [Test]
    public void ScrubTokenValue_NonSensitiveName_StillStripsEmbeddedConnectionStringPassword()
    {
        var result = LogScrubber.ScrubTokenValue("Dsn", "Server=db1;Password=secret", LogHygieneOptions.Default);
        Assert.That(result, Is.EqualTo("Server=db1;Password=***"));
    }

    [Test]
    public void ScrubTokenValue_AllowedName_StillStripsEmbeddedConnectionStringPassword()
    {
        var options = Options(allowTokens: ["Dsn"]);
        var result = LogScrubber.ScrubTokenValue("Dsn", "Server=db1;Password=secret", options);
        Assert.That(result, Is.EqualTo("Server=db1;Password=***"));
    }

    [Test]
    public void ScrubTokenValue_NullOptions_FallsBackToDefaults()
    {
        Assert.That(LogScrubber.ScrubTokenValue("ApiKey", "abc", null), Is.EqualTo("***"));
    }

    // ---- LogHygieneOptions.FromConfiguration ----

    [Test]
    public void FromConfiguration_AbsentBlock_DefaultsLogTokensTrueAndEmptyCollections()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "SomeOtherKey", "x" }
        }).Build();

        var options = LogHygieneOptions.FromConfiguration(config);

        Assert.Multiple(() =>
        {
            Assert.That(options.LogTokens, Is.True);
            Assert.That(options.ScrubTokens, Is.Empty);
            Assert.That(options.ScrubPatterns, Is.Empty);
            Assert.That(options.AllowTokens, Is.Empty);
        });
    }

    [Test]
    public void FromConfiguration_ReadsAllKnobs()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "LogHygiene:LogTokens", "false" },
            { "LogHygiene:ScrubTokens:0", "Handshake" },
            { "LogHygiene:ScrubTokens:1", "TenantSeed" },
            { "LogHygiene:ScrubPatterns:0", "*Salt*" },
            { "LogHygiene:AllowTokens:0", "PublicToken" }
        }).Build();

        var options = LogHygieneOptions.FromConfiguration(config);

        Assert.Multiple(() =>
        {
            Assert.That(options.LogTokens, Is.False);
            Assert.That(options.ScrubTokens, Does.Contain("Handshake"));
            Assert.That(options.ScrubTokens, Does.Contain("TenantSeed"));
            Assert.That(options.ScrubPatterns, Does.Contain("*Salt*"));
            Assert.That(options.AllowTokens, Does.Contain("PublicToken"));
        });
    }

    [Test]
    public void FromConfiguration_ScrubAndAllowTokensAreCaseInsensitive()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "LogHygiene:ScrubTokens:0", "Handshake" },
            { "LogHygiene:AllowTokens:0", "PublicToken" }
        }).Build();

        var options = LogHygieneOptions.FromConfiguration(config);

        Assert.Multiple(() =>
        {
            Assert.That(options.ScrubTokens.Contains("HANDSHAKE"), Is.True);
            Assert.That(options.AllowTokens.Contains("publictoken"), Is.True);
        });
    }
}

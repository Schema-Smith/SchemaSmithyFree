// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Utility;

namespace Schema.UnitTests.Utility
{
    [TestFixture]
    public class CompatEncodingTests
    {
        [TestCase("modern", 100, 10, IngestEncoding.Json)]   // override wins over an old target
        [TestCase("legacy", 160, 16, IngestEncoding.Xml)]    // override wins over a modern target
        [TestCase("auto", 120, 15, IngestEncoding.Xml)]      // compat < 130 -> xml on a modern binary
        [TestCase("auto", 130, 15, IngestEncoding.Json)]     // compat >= 130 -> json
        [TestCase("auto", null, 12, IngestEncoding.Xml)]     // 2014 binary (major < 13), compat unknown -> xml
        [TestCase("auto", null, 13, IngestEncoding.Json)]    // 2016 binary, compat unknown -> json
        [TestCase(null, 130, 16, IngestEncoding.Json)]       // null override behaves as auto
        [TestCase("", 120, 16, IngestEncoding.Xml)]          // empty override behaves as auto
        [TestCase("AUTO", 110, 16, IngestEncoding.Xml)]      // override is case-insensitive
        [TestCase("Legacy", 130, 16, IngestEncoding.Xml)]    // legacy override, case-insensitive
        public void Select_resolves_encoding(string overrideValue, int? compat, int serverMajor, IngestEncoding expected)
            => Assert.That(CompatEncoding.Select(overrideValue, compat, serverMajor), Is.EqualTo(expected));
    }
}

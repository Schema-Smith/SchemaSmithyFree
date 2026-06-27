// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility
{
    [TestFixture]
    public class TargetVersionDetectorTests
    {
        private static IDbCommand CommandReturning(object scalar)
        {
            var cmd = Substitute.For<IDbCommand>();
            cmd.ExecuteScalar().Returns(scalar);
            return cmd;
        }

        [TestCase(Platform.PostgreSQL, "160004", 16)]
        [TestCase(Platform.MySQL, "8.0.36", 800)]
        [TestCase(Platform.SqlServer, "16", 16)]
        public void Detect_ParsesScalar_ToComparable(Platform platform, string raw, int expected)
        {
            var cmd = CommandReturning(raw);

            var info = TargetVersionDetector.Detect(cmd, platform);

            Assert.That(info.Platform, Is.EqualTo(platform));
            Assert.That(info.RawVersion, Is.EqualTo(raw));
            Assert.That(info.ServerComparable, Is.EqualTo(expected));
        }

        [Test]
        public void Detect_Throws_WhenScalarNull()
        {
            var cmd = CommandReturning(null);

            var ex = Assert.Throws<Exception>(() => TargetVersionDetector.Detect(cmd, Platform.PostgreSQL));
            Assert.That(ex!.Message, Does.Contain("Unable to determine"));
        }

        [Test]
        public void GetVersionQuery_IsPlatformSpecific()
        {
            Assert.That(TargetVersionDetector.GetVersionQuery(Platform.SqlServer), Does.Contain("ProductMajorVersion"));
            Assert.That(TargetVersionDetector.GetVersionQuery(Platform.PostgreSQL), Does.Contain("server_version_num"));
            Assert.That(TargetVersionDetector.GetVersionQuery(Platform.MySQL), Does.Contain("VERSION()"));
        }
    }
}

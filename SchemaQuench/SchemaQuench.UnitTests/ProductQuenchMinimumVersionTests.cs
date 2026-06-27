// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.UnitTests
{
    [TestFixture]
    public class ProductQuenchMinimumVersionTests
    {
        [Test]
        public void BuildMinimumVersionFailures_FlagsBelowFloorServers_Only()
        {
            var detected = new List<(string, TargetVersionInfo)>
            {
                ("primary", new TargetVersionInfo(Platform.PostgreSQL, "150010", 15)),
                ("secondary", new TargetVersionInfo(Platform.PostgreSQL, "160004", 16))
            };

            var failures = ProductQuench.BuildMinimumVersionFailures(detected, requiredComparable: 16, declaredMinimum: "16");

            Assert.That(failures, Has.Count.EqualTo(1));
            Assert.That(failures.Single(), Does.Contain("primary"));
            Assert.That(failures.Single(), Does.Contain("150010"));
            Assert.That(failures.Single(), Does.Contain("16"));
        }

        [Test]
        public void BuildMinimumVersionFailures_Empty_WhenAllMeetFloor()
        {
            var detected = new List<(string, TargetVersionInfo)>
            {
                ("primary", new TargetVersionInfo(Platform.SqlServer, "16", 16))
            };

            var failures = ProductQuench.BuildMinimumVersionFailures(detected, requiredComparable: 15, declaredMinimum: "2019");

            Assert.That(failures, Is.Empty);
        }
    }
}

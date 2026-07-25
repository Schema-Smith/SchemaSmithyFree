// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility
{
    [TestFixture]
    public class PreFlightVersionGuardTests
    {
        [Test]
        public void CheckOrThrow_BelowServerFloor_Throws_WithClearMessage()
        {
            var info = new TargetVersionInfo(Platform.SqlServer, "11", 11);   // SQL Server 2012

            var ex = Assert.Throws<Exception>(() =>
                PreFlightVersionGuard.CheckOrThrow(info, "SQL2K12\\SC2K12", "Chinook"));

            Assert.That(ex!.Message, Does.Contain("below the minimum supported"));
            Assert.That(ex.Message, Does.Contain("SQL2K12"));
        }

        [Test]
        public void CheckOrThrow_CompatBelow140_Throws_DistinctMessage()
        {
            var info = new TargetVersionInfo(Platform.SqlServer, "14", 14, 130);

            var ex = Assert.Throws<Exception>(() =>
                PreFlightVersionGuard.CheckOrThrow(info, "srv", "OldDb"));

            Assert.That(ex!.Message, Does.Contain("compatibility level 130"));
            Assert.That(ex.Message, Does.Contain("OldDb"));
        }

        [Test]
        public void CheckOrThrow_SupportedServerAndCompat_DoesNotThrow()
        {
            var info = new TargetVersionInfo(Platform.SqlServer, "14", 14, 140);

            Assert.DoesNotThrow(() => PreFlightVersionGuard.CheckOrThrow(info, "srv", "Db"));
        }

        [Test]
        public void CheckOrThrow_CompatNull_SkipsCompatCheck()
        {
            var info = new TargetVersionInfo(Platform.SqlServer, "14", 14);   // compat not detected

            Assert.DoesNotThrow(() => PreFlightVersionGuard.CheckOrThrow(info, "srv"));
        }

        [Test]
        public void CheckOrThrow_PostgresBelowFloor_Throws()
        {
            var info = new TargetVersionInfo(Platform.PostgreSQL, "140006", 14);

            var ex = Assert.Throws<Exception>(() => PreFlightVersionGuard.CheckOrThrow(info, "pg"));
            Assert.That(ex!.Message, Does.Contain("below the minimum supported"));
        }

        [Test]
        public void CheckOrThrow_MariaDbAtFloor_DoesNotThrow()
        {
            var info = new TargetVersionInfo(Platform.MariaDb, "10.6.27-MariaDB", 1006);

            Assert.DoesNotThrow(() => PreFlightVersionGuard.CheckOrThrow(info, "maria"));
        }
    }
}

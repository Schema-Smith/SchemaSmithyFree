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
            var info = new TargetVersionInfo(Platform.SqlServer, "9", 9);   // SQL Server 2005 — below the 2008 floor

            var ex = Assert.Throws<Exception>(() =>
                PreFlightVersionGuard.CheckOrThrow(info, "SQL2K5\\SC2K5", "Chinook"));

            Assert.That(ex!.Message, Does.Contain("below the minimum supported"));
            Assert.That(ex.Message, Does.Contain("SQL2K5"));
        }

        [Test]
        public void CheckOrThrow_CompatBelow100_Throws_DistinctMessage()
        {
            var info = new TargetVersionInfo(Platform.SqlServer, "10", 10, 90);   // 2008 binary, compat-90 DB

            var ex = Assert.Throws<Exception>(() =>
                PreFlightVersionGuard.CheckOrThrow(info, "srv", "OldDb"));

            Assert.That(ex!.Message, Does.Contain("compatibility level 90"));
            Assert.That(ex.Message, Does.Contain("OldDb"));
        }

        [Test]
        public void CheckOrThrow_CompatAtFloor100_DoesNotThrow()
        {
            // compat 100 (SQL Server 2008) is the floor: below compat 130 the model is ingested/compared
            // as XML (OPENJSON's JSON-path parse-errors below 130), so a compat-100 DB is supported.
            var info = new TargetVersionInfo(Platform.SqlServer, "10", 10, 100);

            Assert.DoesNotThrow(() => PreFlightVersionGuard.CheckOrThrow(info, "srv", "Db"));
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
            var info = new TargetVersionInfo(Platform.PostgreSQL, "110006", 11);   // PostgreSQL 11 — below the 12 floor

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

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain.MariaDb;
using Schema.Domain.MySQL;

namespace Schema.UnitTests.Domain.MariaDb
{
    /// <summary>
    /// <see cref="MariaDbColumn"/> — the first MariaDB-specific column type (#408).
    /// <para>It exists for one attribute MySQL has no equivalent for. The tests that matter here are the
    /// two directions of leakage: the property must survive a round trip, and it must NOT appear on a
    /// MySQL package, because a MySQL server rejects the clause at any version.</para>
    /// </summary>
    [TestFixture]
    public class MariaDbColumnTests
    {
        [Test]
        public void InheritsFromMySqlColumn()
        {
            Assert.That(new MariaDbColumn(), Is.InstanceOf<MySqlColumn>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            Assert.That(new MariaDbColumn().WithoutSystemVersioning, Is.False,
                "a column must not be excluded from history unless the package says so");
        }

        [Test]
        public void JsonRoundTrip_PreservesWithoutSystemVersioning()
        {
            var column = new MariaDbColumn
            {
                Name = "payload",
                DataType = "int(11)",
                Nullable = true,
                WithoutSystemVersioning = true,
                Comment = "high churn, deliberately unversioned"
            };

            var deserialized = JsonConvert.DeserializeObject<MariaDbColumn>(JsonConvert.SerializeObject(column));

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.WithoutSystemVersioning, Is.True,
                    "losing this on a round trip is the defect #408 is about -- the redeployed column "
                    + "silently starts accumulating history");
                Assert.That(deserialized.Comment, Is.EqualTo("high churn, deliberately unversioned"),
                    "and the inherited MySQL properties still work");
            });
        }

        [Test]
        public void AMySqlColumn_DoesNotCarryTheProperty()
        {
            // The leakage direction that would break deploys rather than merely add noise: MySQL rejects
            // WITHOUT SYSTEM VERSIONING at every version, so it must never reach a MySQL package.
            var json = JsonConvert.SerializeObject(new MySqlColumn { Name = "payload", DataType = "int(11)" });

            Assert.That(json, Does.Not.Contain("WithoutSystemVersioning"), json);
        }
    }
}

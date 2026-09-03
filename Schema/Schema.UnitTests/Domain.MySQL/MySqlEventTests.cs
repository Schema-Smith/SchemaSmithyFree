// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain.MySQL;

namespace Schema.UnitTests.Domain.MySQL
{
    /// <summary>
    /// <see cref="MySqlEvent"/> — the declarative form of a MySQL/MariaDB scheduled event (F4).
    /// <para>The defaults carry real weight here. <c>Preserve</c> must default FALSE to match the engine's
    /// own <c>NOT PRESERVE</c>, and <c>Status</c> must default to the DDL spelling <c>ENABLE</c> rather
    /// than the catalog's <c>ENABLED</c> — the package speaks the language an author writes, and every
    /// comparison against the catalog translates rather than assuming they agree.</para>
    /// </summary>
    [TestFixture]
    public class MySqlEventTests
    {
        [Test]
        public void DefaultValues_MatchTheEngine()
        {
            var ev = new MySqlEvent();

            Assert.Multiple(() =>
            {
                Assert.That(ev.ScheduleType, Is.EqualTo("EVERY"), "recurring is the common case");
                Assert.That(ev.Status, Is.EqualTo("ENABLE"), "the DDL spelling, not the catalog's ENABLED");
                Assert.That(ev.Preserve, Is.False, "the engine's own default is NOT PRESERVE");
                Assert.That(ev.Interval, Is.Null);
                Assert.That(ev.Starts, Is.Null);
            });
        }

        [Test]
        public void JsonRoundTrip_PreservesEveryProperty()
        {
            var ev = new MySqlEvent
            {
                Name = "nightly_purge",
                Definition = "DELETE FROM audit WHERE created < NOW() - INTERVAL 90 DAY",
                ScheduleType = "EVERY",
                Interval = "1 DAY",
                Starts = "2026-01-01 02:00:00",
                Ends = "2027-01-01 02:00:00",
                Status = "DISABLE",
                Preserve = true,
                Comment = "retention"
            };

            var back = JsonConvert.DeserializeObject<MySqlEvent>(JsonConvert.SerializeObject(ev));

            Assert.Multiple(() =>
            {
                Assert.That(back.Name, Is.EqualTo("nightly_purge"));
                Assert.That(back.Definition, Does.Contain("DELETE FROM audit"));
                Assert.That(back.Interval, Is.EqualTo("1 DAY"));
                Assert.That(back.Starts, Is.EqualTo("2026-01-01 02:00:00"));
                Assert.That(back.Ends, Is.EqualTo("2027-01-01 02:00:00"));
                Assert.That(back.Status, Is.EqualTo("DISABLE"));
                Assert.That(back.Preserve, Is.True);
                Assert.That(back.Comment, Is.EqualTo("retention"));
            });
        }

        [Test]
        public void AnUnsetOptionalProperty_IsNotSerialized()
        {
            // Extraction emits only what the server actually reports, and a one-shot event has no
            // Interval while a recurring one has no ExecuteAt. Writing nulls for those would put keys in
            // the package that its schema declares as strings and the author never wrote.
            var json = JsonConvert.SerializeObject(new MySqlEvent { Name = "e", Definition = "SET @x = 1" });

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Not.Contain("Interval"), json);
                Assert.That(json, Does.Not.Contain("ExecuteAt"), json);
                Assert.That(json, Does.Not.Contain("Starts"), json);
                Assert.That(json, Does.Not.Contain("Comment"), json);
            });
        }
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using Newtonsoft.Json.Linq;
using Schema.Domain.MariaDb;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.UnitTests.Domain.MariaDb;

/// <summary>
/// A MariaDB-only property must not reach MySQL's generated table schema.
/// <para>This is the whole reason <see cref="MariaDbTable"/> exists. Before it, MariaDB deserialized to
/// <see cref="MySqlTable"/>, so the only place to put <c>IsSystemVersioned</c> would have been the
/// shared type — and it would then have appeared in <c>tables.mysql.schema</c>, where an editor would
/// green-light a setting MySQL has no concept of at any version.</para>
/// <para>Asserted on the <b>generated schema</b> rather than on the type, because the schema is what a
/// user's editor actually reads. A test that only checked which class declares the property would pass
/// even if the generator flattened the hierarchy and emitted it for both.</para>
/// <para>This test was deliberately not written when <c>MariaDbTable</c> was introduced empty: with no
/// MariaDB-only property in existence it would have passed vacuously, proving nothing.</para>
/// </summary>
[TestFixture]
public class MariaDbTableSchemaIsolationTests
{
    private static JObject SchemaFor(System.Type t) => SchemaGenerator.GenerateSchema(t);

    private static bool Mentions(JObject schema, string propertyName) =>
        schema.DescendantsAndSelf()
            .OfType<JProperty>()
            .Any(p => p.Name == propertyName);

    [Test]
    public void MariaDbOnlyProperties_AppearInMariaDbSchema_AndNotInMySqlSchema()
    {
        var mariaOnly = typeof(MariaDbTable)
            .GetProperties(System.Reflection.BindingFlags.Public
                           | System.Reflection.BindingFlags.Instance
                           | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .ToList();

        // Guards the premise. If MariaDbTable ever declares nothing of its own again, this test proves
        // nothing and must say so rather than passing quietly -- the exact failure it exists to prevent.
        Assert.That(mariaOnly, Is.Not.Empty,
            "MariaDbTable declares no properties of its own, so this test cannot demonstrate isolation. "
            + "Either a MariaDB-only property was removed, or it was moved up to MySqlTable -- which is "
            + "the leak this fixture guards against.");

        var mariaSchema = SchemaFor(typeof(MariaDbTable));
        var mySqlSchema = SchemaFor(typeof(MySqlTable));

        foreach (var name in mariaOnly)
        {
            Assert.That(Mentions(mariaSchema, name), Is.True,
                $"'{name}' is declared on MariaDbTable but never reaches tables.mariadb.schema, so a "
                + "MariaDB user's editor would reject a package that legitimately sets it.");

            Assert.That(Mentions(mySqlSchema, name), Is.False,
                $"'{name}' is MariaDB-only but leaked into MySQL's generated schema. A MySQL user's "
                + "editor would green-light a setting the engine cannot honour, which is precisely what "
                + "splitting MariaDbTable out of MySqlTable was meant to prevent.");
        }
    }
}

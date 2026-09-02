// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Domain.MariaDb;
using Schema.Domain.MySQL;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;
using Schema.Utility;

namespace Schema.UnitTests.Domain.SqlServer;

/// <summary>
/// A SQL-Server-only table property must not reach any other engine's generated table schema.
/// <para>The sibling of <c>MariaDbTableSchemaIsolationTests</c>, and written because the mistake it
/// guards against has actually been made: a MariaDB-only setting was first put on the shared
/// <c>Table</c> type, which silently added it to 434 SQL Server package schemas. Nothing failed — the
/// leak is invisible until someone notices an editor offering a setting the engine has no concept of.
/// </para>
/// <para>Asserted on the <b>generated schema</b> rather than on the declaring type, because the schema
/// is what a user's editor actually reads. A test that only checked which class declares a property
/// would pass even if the generator flattened the hierarchy and emitted it everywhere.</para>
/// </summary>
[TestFixture]
public class SqlServerTableSchemaIsolationTests
{
    private static JObject SchemaFor(Type t) => SchemaGenerator.GenerateSchema(t);

    private static bool Mentions(JObject schema, string propertyName) =>
        schema.DescendantsAndSelf().OfType<JProperty>().Any(p => p.Name == propertyName);

    [Test]
    public void SqlServerOnlyProperties_AppearInSqlServerSchema_AndNotInAnyOtherEngineSchema()
    {
        // [JsonIgnore] properties are excluded on purpose: SqlServerTable's IDeliverableTable members
        // (DeliverableColumns, DeliverableForeignKeys) are computed views over Columns/ForeignKeys for
        // internal consumers and are deliberately never serialized, so they belong in no schema at all.
        var sqlServerOnly = typeof(SqlServerTable)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .Select(p => p.Name)
            .ToList();

        // Guards the premise -- an empty list would make every assertion below vacuous.
        Assert.That(sqlServerOnly, Is.Not.Empty,
            "SqlServerTable declares no properties of its own, so this test cannot demonstrate "
            + "isolation. Either they were removed, or moved up to the shared Table type -- which is "
            + "the leak this fixture exists to catch.");

        var sqlServerSchema = SchemaFor(typeof(SqlServerTable));
        // A name declared on SqlServerTable is not automatically SQL-Server-only: PostgreSQL declares
        // its own Schema, Statistics and UpdateFillFactor, because those concepts genuinely exist on
        // both engines. Only a name the other engine's own table type does NOT have can be a leak.
        var others = new (string Engine, Type Type, JObject Schema)[]
        {
            ("PostgreSQL", typeof(PostgreSqlTable), SchemaFor(typeof(PostgreSqlTable))),
            ("MySQL", typeof(MySqlTable), SchemaFor(typeof(MySqlTable))),
            ("MariaDB", typeof(MariaDbTable), SchemaFor(typeof(MariaDbTable))),
        };

        Assert.Multiple(() =>
        {
            foreach (var name in sqlServerOnly)
            {
                Assert.That(Mentions(sqlServerSchema, name), Is.True,
                    $"'{name}' is declared on SqlServerTable but never reaches tables.sqlserver.schema, "
                    + "so a SQL Server user's editor would reject a package that legitimately sets it.");

                foreach (var (engine, type, schema) in others)
                {
                    if (type.GetProperty(name) != null) continue;

                    Assert.That(Mentions(schema, name), Is.False,
                        $"'{name}' is SQL-Server-only but leaked into {engine}'s generated schema. That "
                        + $"engine's users would be offered a setting it has no concept of, and every "
                        + $"committed {engine} package schema would carry it.");
                }
            }
        });
    }
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Linq;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;

namespace Schema.Domain
{
    /// <summary>
    /// Resolves per-platform schema defaults after JSON deserialization, before DDL generation.
    /// Regular templates: unset Schema / RelatedTableSchema resolve to the platform default
    /// ("dbo" on SQL Server, "public" on PostgreSQL — matching pre-refactor behavior bit-for-bit).
    /// Schema templates: unset values resolve to the "{{SchemaName}}" token; literal Schema
    /// values on tables / indexed views / materialized views are rejected (the object lives in
    /// {{SchemaName}} by construction). Literal RelatedTableSchema values on FKs are preserved
    /// as cross-schema references.
    /// </summary>
    public static class SchemaDefaultResolver
    {
        public const string SchemaNameToken = "{{SchemaName}}";

        public static void Resolve(SqlServerTable table, bool isSchemaTemplate, Platform platform)
        {
            if (table == null) return;
            table.Schema = ResolveTableSchema(table.Schema, table.Name, "table", isSchemaTemplate, platform);
            foreach (var fk in table.ForeignKeys.OfType<SqlServerForeignKey>())
                Resolve(fk, isSchemaTemplate, platform);
        }

        public static void Resolve(PostgreSqlTable table, bool isSchemaTemplate, Platform platform)
        {
            if (table == null) return;
            table.Schema = ResolveTableSchema(table.Schema, table.Name, "table", isSchemaTemplate, platform);
            foreach (var fk in table.ForeignKeys.OfType<PostgreSqlForeignKey>())
                Resolve(fk, isSchemaTemplate, platform);
        }

        public static void Resolve(SqlServerForeignKey fk, bool isSchemaTemplate, Platform platform)
        {
            if (fk == null) return;
            fk.RelatedTableSchema = ResolveRelatedTableSchema(fk.RelatedTableSchema, isSchemaTemplate, platform);
        }

        public static void Resolve(PostgreSqlForeignKey fk, bool isSchemaTemplate, Platform platform)
        {
            if (fk == null) return;
            fk.RelatedTableSchema = ResolveRelatedTableSchema(fk.RelatedTableSchema, isSchemaTemplate, platform);
        }

        public static void Resolve(SqlServerIndexedView view, bool isSchemaTemplate, Platform platform)
        {
            if (view == null) return;
            view.Schema = ResolveTableSchema(view.Schema, view.Name, "indexed view", isSchemaTemplate, platform);
        }

        public static void Resolve(PostgreSqlMaterializedView view, bool isSchemaTemplate, Platform platform)
        {
            if (view == null) return;
            view.Schema = ResolveTableSchema(view.Schema, view.Name, "materialized view", isSchemaTemplate, platform);
        }

        /// <summary>
        /// Resolves all platform-typed children of a template in one pass. Called from
        /// Template.Load after deserialization completes.
        /// </summary>
        public static void Resolve(Template template)
        {
            if (template == null) return;
            var isSchemaTemplate = template.IsSchemaTemplate;
            var platform = template.Product?.Platform ?? Platform.Unknown;
            if (platform == Platform.Unknown) return;

            foreach (var table in template.Tables)
            {
                switch (table)
                {
                    case SqlServerTable sst: Resolve(sst, isSchemaTemplate, platform); break;
                    case PostgreSqlTable pgt: Resolve(pgt, isSchemaTemplate, platform); break;
                    // MySqlTable: no schema defaulting (MySQL has no namespace concept).
                }
            }

            foreach (var view in template.IndexedViews)
                Resolve(view, isSchemaTemplate, platform);

            foreach (var view in template.MaterializedViews)
                Resolve(view, isSchemaTemplate, platform);
        }

        private static string ResolveTableSchema(
            string existing, string ownerName, string ownerKind, bool isSchemaTemplate, Platform platform)
        {
            if (isSchemaTemplate)
            {
                if (string.IsNullOrWhiteSpace(existing) || existing == SchemaNameToken)
                    return SchemaNameToken;

                throw new InvalidOperationException(
                    $"The {ownerKind} '{ownerName}' in a schema template has an explicit Schema='{existing}'. " +
                    $"Schema templates require this field to be omitted, empty, or the literal '{SchemaNameToken}' " +
                    $"— the {ownerKind} lives in {SchemaNameToken} by construction. " +
                    $"Move shared {ownerKind}s to a non-schema-template (which runs first in TemplateOrder).");
            }

            return string.IsNullOrWhiteSpace(existing) ? platform.GetDefaultSchema() : existing;
        }

        private static string ResolveRelatedTableSchema(string existing, bool isSchemaTemplate, Platform platform)
        {
            if (isSchemaTemplate)
            {
                // FK RelatedTableSchema is permissive: unset → token; literal token → token (no-op);
                // literal hard value → preserved as a cross-schema reference.
                return string.IsNullOrWhiteSpace(existing) ? SchemaNameToken : existing;
            }

            return string.IsNullOrWhiteSpace(existing) ? platform.GetDefaultSchema() : existing;
        }
    }
}

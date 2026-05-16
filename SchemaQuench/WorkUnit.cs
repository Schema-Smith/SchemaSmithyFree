// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaQuench;

/// <summary>
/// An atomic unit of work dispatched by <see cref="WorkUnitDispatcher"/>: one
/// <c>(server, database, template, schema)</c> tuple. The pipeline executes each
/// work unit independently, with at most <c>MaxThreads</c> running concurrently
/// (subject to per-template serial-queue carveouts via <c>Template.AllowParallel</c>).
///
/// <para>
/// For regular (non-schema) templates, <see cref="SchemaName"/> is the empty string —
/// the template runs once per database with <c>{{SchemaName}}</c> undefined. For
/// schema templates, <see cref="SchemaName"/> is one of the names returned by the
/// template's <c>SchemaIdentificationScript</c> (design §5.1).
/// </para>
/// </summary>
/// <param name="Server">Target database server identifier (host name, ServerToQuench resolution).</param>
/// <param name="DatabaseName">Target database name as returned by <c>DatabaseIdentificationScript</c>.</param>
/// <param name="TemplateName">The template this unit executes — also the key for the AllowParallel map.</param>
/// <param name="SchemaName">
/// The iteration schema for schema templates, or empty string for regular templates.
/// Empty-string sentinel is intentional: it matches the persisted migration-tracking
/// schema-name column convention (slice 2) where regular-template rows store '' rather than NULL.
/// </param>
public sealed record WorkUnit(
    string Server,
    string DatabaseName,
    string TemplateName,
    string SchemaName);

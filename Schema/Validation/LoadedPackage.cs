// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Schema.Domain;

namespace Schema.Validation;

/// <summary>
/// A fully loaded schema package: the <see cref="Product"/> plus every <see cref="Template"/>
/// named in its TemplateOrder. Templates are not attached to Product (mirrors
/// <c>ProductQuench.LoadTemplates</c>) — this record carries them alongside it so
/// <see cref="ValidationContext"/> and in-memory test fixtures don't need a live Product/disk
/// round-trip to assemble a package.
/// <para>
/// <paramref name="LoadFindings"/> carries findings produced DURING loading itself — currently
/// just SS-LOAD-001 for a component file PackageLoader's lenient load skipped because it wasn't
/// valid JSON at all (see <see cref="Template.ComponentLoadErrors"/>). Defaults to null/empty so
/// existing in-memory test fixtures that construct a <see cref="LoadedPackage"/> directly (no
/// disk load, nothing to skip) don't need updating.
/// </para>
/// </summary>
public sealed record LoadedPackage(Product Product, IReadOnlyList<Template> Templates, IReadOnlyList<Finding> LoadFindings = null);

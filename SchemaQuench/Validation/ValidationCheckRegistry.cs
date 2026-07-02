// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;

namespace SchemaQuench.Validation;

/// <summary>
/// Source of the checks run by `--Validate`. Empty in Slice 1 — the gate only exercises the
/// package-load path (SS-LOAD-001). Real <see cref="ISchemaCheck"/> implementations are appended
/// here in Slice 2.
/// </summary>
public static class ValidationCheckRegistry
{
    public static IReadOnlyList<ISchemaCheck> Default() => new List<ISchemaCheck>();
}

// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;

namespace Schema.Validation;

/// <summary>
/// One `--Validate` linter rule. Implementations inspect the loaded package via
/// <see cref="ValidationContext"/> and return their findings; they are not expected to throw
/// (the orchestrator deliberately has no per-check try/catch — see <see cref="SchemaPackageValidator"/>).
/// </summary>
public interface ISchemaCheck
{
    IEnumerable<Finding> Run(ValidationContext ctx);
}

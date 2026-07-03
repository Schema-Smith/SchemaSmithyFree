// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaQuench.Validation;

public sealed record Finding(Severity Severity, string Code, string Category, string Location, string Message);

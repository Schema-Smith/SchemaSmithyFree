// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Domain;

/// <summary>
/// Records a <c>&lt;*File*&gt;</c>/<c>&lt;*BinaryFile*&gt;</c>/<c>&lt;*QueryFile*&gt;</c> script
/// token that <see cref="Product.Load"/> / <see cref="Template.Load"/> could not resolve, captured
/// instead of thrown when the caller passes <c>tolerateFileTokenErrors: true</c> (--Validate's
/// lenient load). <see cref="FilePath"/> is the manifest that declared the token (Product.json or
/// Template.json); <see cref="Message"/> is <see cref="Schema.Utility.TokenHelper"/>'s own error
/// text, which already names the token key and the full resolved path it looked for.
/// </summary>
public sealed record FileTokenError(string FilePath, string Message);

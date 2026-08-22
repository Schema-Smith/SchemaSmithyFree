// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace Schema.Configuration;

/// <summary>
/// Names the tool that actually reads a settings key.
/// </summary>
/// <remarks>
/// The ShouldCast keys are declared in one place but read by two different tools, and the contract used to
/// hand the whole set to both. That let a real key sit in the wrong tool's settings file doing nothing, with
/// no warning -- the precise failure the unrecognised-key check exists to catch, missed inside its own
/// problem space. A genuine typo still warned; copying a block between two settings files that look alike
/// did not, which is the likelier mistake.
///
/// Recording the reader beside the constant keeps the fact where it can be maintained, rather than in a
/// hand-kept list somewhere else that drifts the first time a key is added.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ReadByAttribute(SettingsTool tool) : Attribute
{
    public SettingsTool Tool { get; } = tool;
}

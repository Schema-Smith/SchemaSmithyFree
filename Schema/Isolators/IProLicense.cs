// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using SchemaSmith.Pro;

namespace Schema.Isolators;

public interface IProLicense
{
    bool IsLicensed { get; }
    string LicenseDisplayText { get; }
    List<LicenseCommandLineOption> GetAdditionalCommandLineOptions(string toolName);
    string FormatProOptions(string toolName);
    string GetLicenseDisplayText();
}

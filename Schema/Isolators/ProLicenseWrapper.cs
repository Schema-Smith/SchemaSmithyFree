// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using SchemaSmith.Pro;

namespace Schema.Isolators;

public class ProLicenseWrapper : IProLicense
{
    public bool IsLicensed => ProServices.License.IsLicensed;
    public string LicenseDisplayText => ProServices.License.LicenseDisplayText;

    public List<LicenseCommandLineOption> GetAdditionalCommandLineOptions(string toolName)
        => ProServices.License.GetAdditionalCommandLineOptions(toolName);

    public string FormatProOptions(string toolName)
        => ProServices.FormatProOptions(toolName);

    public string GetLicenseDisplayText()
        => ProServices.GetLicenseDisplayText();

    public static IProLicense GetFromFactory()
        => FactoryContainer.ResolveOrCreate<IProLicense, ProLicenseWrapper>();
}

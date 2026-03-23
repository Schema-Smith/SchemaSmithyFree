// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Reflection;

namespace SchemaHammer.ViewModels;

public class AboutViewModel
{
    public string AppName => "SchemaHammer Community";
    public string Version => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "2.0.0";
    public string Description => "A read-only schema viewer for SchemaSmith products";
    public string GitHubUrl => "https://github.com/pwhittin/SchemaSmithyFree";
}

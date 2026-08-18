// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Schema.Domain;
using Schema.Utility;
using Schema.Configuration;

namespace Schema.DataAccess;

/// <summary>
/// Builds a target connection string from configuration, applying every concern a target connection
/// carries: <c>Target:User</c>/<c>Password</c>/<c>Port</c>, <c>Target:ConnectionProperties</c>, the
/// <c>-Encrypt</c>/<c>-NoEncrypt</c> transport switch, and <c>Target:IntegratedSecurity</c>.
/// <para>Every caller that opens a target connection goes through here. The server-level connection
/// test and the per-database work units previously assembled their own strings, and the work-unit
/// copy silently omitted two of these concerns — so an integrated-security or -NoEncrypt run passed
/// its connection test and then failed at deploy.</para>
/// </summary>
public static class TargetConnectionString
{
    public static string Build(Platform platform, string server, string database, IConfigurationRoot config)
    {
        return ConnectionString.Build(platform, server, database,
            config[SettingsKeys.Target.User], config[SettingsKeys.Target.Password], config[SettingsKeys.Target.Port],
            ReadConnectionProperties(platform, config),
            integratedSecurity: IsIntegratedSecurity(config));
    }

    /// <summary>
    /// Reads <c>Target:ConnectionProperties</c> and applies the <c>-Encrypt</c>/<c>-NoEncrypt</c>
    /// transport-security switch for <paramref name="platform"/>, so every target connection a run
    /// builds honors the flag consistently.
    /// </summary>
    public static Dictionary<string, string> ReadConnectionProperties(Platform platform, IConfigurationRoot config)
    {
        var props = ConnectionString.ReadProperties(config, SettingsKeys.Target.ConnectionProperties);
        CommandLineParser.ApplyTransportSecuritySwitch(platform, props);
        return props;
    }

    // Integrated Security is opt-in via Target:IntegratedSecurity=true. It supersedes any configured
    // Target:User/Password rather than requiring them to be cleared — an override cannot clear a
    // credential a settings file carries (setting an env var to empty deletes it on Windows), so a
    // checked-in "User": "sa" would otherwise block Windows Authentication. SQL Server only.
    public static bool IsIntegratedSecurity(IConfigurationRoot config) =>
        string.Equals(config[SettingsKeys.Target.IntegratedSecurity], "true", StringComparison.OrdinalIgnoreCase);
}
